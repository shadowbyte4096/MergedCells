-- Chunk 34: reading each level's spread back, and a per-player lock on the sending functions (see docs/systems/server.md).

-- How many players' bests took each number of moves on one level and layout: the 200 lowest move counts, and how many
-- players are above them. Counts only, never rows, player IDs, times or sign-ins
create or replace function public.best_moves_spread(level integer, layout text)
returns jsonb
language plpgsql
stable
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  listed jsonb;
  beyond bigint;
begin
  if auth.uid() is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if best_moves_spread.level is null or best_moves_spread.layout is null then
    raise exception 'A level and a layout are needed.' using errcode = '22023';
  end if;
  with grouped as (
    select s.moves, count(*)::integer as players, row_number() over (order by s.moves) as place
    from game.scores s
    where s.level = best_moves_spread.level and s.layout = best_moves_spread.layout
    group by s.moves)
  select coalesce(jsonb_agg(jsonb_build_array(g.moves, g.players) order by g.moves) filter (where g.place <= 200), '[]'::jsonb),
         coalesce(sum(g.players) filter (where g.place > 200), 0)
    into listed, beyond
    from grouped g;
  return jsonb_build_object('counts', listed, 'more', beyond);
end;
$$;
revoke execute on function public.best_moves_spread(integer, text) from public, anon, authenticated;
grant execute on function public.best_moves_spread(integer, text) to authenticated;

-- The three sending functions as in 20261119120000_leaderboards.sql, each now taking a lock on its player first, so a
-- first send still running when delete_player commits can't survive the deletion (chunk 33's final review)
create or replace function public.submit_scores(player uuid, scores jsonb)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  caller uuid := auth.uid();
  entry jsonb;
  entry_level integer;
  entry_layout text;
  entry_moves integer;
  level_par integer;
  accepted integer := 0;
  unknown jsonb := '[]'::jsonb;
  below_par jsonb := '[]'::jsonb;
begin
  if caller is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if submit_scores.player is null or submit_scores.scores is null or jsonb_typeof(submit_scores.scores) <> 'array' then
    raise exception 'A player and an array of scores are needed.' using errcode = '22023';
  end if;
  if jsonb_array_length(submit_scores.scores) > 500 then
    raise exception 'At most 500 scores at a time.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(submit_scores.player::text, 0));
  if game.is_deleted(submit_scores.player) then
    return jsonb_build_object('accepted', 0, 'playerDeleted', true);
  end if;

  insert into game.players (id) values (submit_scores.player)
    on conflict (id) do update set last_seen = now();
  for entry in select value from jsonb_array_elements(submit_scores.scores) loop
    begin
      entry_level := (entry->>'level')::integer;
      entry_layout := entry->>'layout';
      entry_moves := (entry->>'moves')::integer;
    exception when others then
      entry_level := null;
    end;
    -- An entry that doesn't read is skipped unlisted: the game never sends one
    continue when entry_level is null or entry_layout is null or entry_moves is null;
    select l.par into level_par from game.levels l where l.id = entry_level and l.layout = entry_layout;
    if level_par is null then
      unknown := unknown || to_jsonb(entry_level);
    elsif entry_moves < level_par then
      below_par := below_par || to_jsonb(entry_level);
    else
      insert into game.scores as s (player, level, layout, moves, auth_user)
        values (submit_scores.player, entry_level, entry_layout, entry_moves, caller)
        on conflict (player, level, layout) do update
          set moves = least(s.moves, excluded.moves), auth_user = excluded.auth_user, updated_at = now();
      accepted := accepted + 1;
    end if;
  end loop;
  return jsonb_build_object('accepted', accepted, 'unknown', unknown, 'belowPar', below_par);
end;
$$;
revoke execute on function public.submit_scores(uuid, jsonb) from public, anon, authenticated;
grant execute on function public.submit_scores(uuid, jsonb) to authenticated;

create or replace function public.upload_records(player uuid, records jsonb)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  caller uuid := auth.uid();
  entry jsonb;
  record_id uuid;
  record_started timestamptz;
  added integer;
  stored integer := 0;
  duplicates integer := 0;
  skipped integer := 0;
begin
  if caller is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if upload_records.player is null or upload_records.records is null or jsonb_typeof(upload_records.records) <> 'array' then
    raise exception 'A player and an array of records are needed.' using errcode = '22023';
  end if;
  if jsonb_array_length(upload_records.records) > 100 then
    raise exception 'At most 100 records at a time.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(upload_records.player::text, 0));
  if game.is_deleted(upload_records.player) then
    return jsonb_build_object('stored', 0, 'playerDeleted', true);
  end if;

  insert into game.players (id) values (upload_records.player)
    on conflict (id) do update set last_seen = now();
  for entry in select value from jsonb_array_elements(upload_records.records) loop
    begin
      if jsonb_typeof(entry) <> 'object'
          or (entry->>'player') is distinct from upload_records.player::text
          or coalesce((entry->>'consent')::integer, 0) < 1
          or octet_length(entry::text) > 1048576 then
        skipped := skipped + 1;
        continue;
      end if;
      record_id := (entry->>'id')::uuid;
      record_started := (entry->>'started')::timestamptz;
      if record_id is null or record_started is null then
        skipped := skipped + 1;
        continue;
      end if;
      insert into game.play_records (id, player, started, record, auth_user)
        values (record_id, upload_records.player, record_started, entry, caller)
        on conflict (id) do nothing;
      get diagnostics added = row_count;
      if added = 1 then
        stored := stored + 1;
      else
        duplicates := duplicates + 1;
      end if;
    exception when others then
      skipped := skipped + 1;
    end;
  end loop;
  return jsonb_build_object('stored', stored, 'duplicates', duplicates, 'skipped', skipped);
end;
$$;
revoke execute on function public.upload_records(uuid, jsonb) from public, anon, authenticated;
grant execute on function public.upload_records(uuid, jsonb) to authenticated;

create or replace function public.delete_player(player uuid)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  senders uuid[];
  removed integer;
begin
  if delete_player.player is null then
    raise exception 'A player is needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(delete_player.player::text, 0));
  select array_agg(sender) into senders from (
    select s.auth_user as sender from game.scores s where s.player = delete_player.player
    union
    select r.auth_user from game.play_records r where r.player = delete_player.player) sent;
  delete from game.players p where p.id = delete_player.player;
  get diagnostics removed = row_count;
  delete from auth.users u
    where u.id = any(coalesce(senders, '{}'::uuid[]))
      and u.is_anonymous
      and not exists (select 1 from game.scores s where s.auth_user = u.id)
      and not exists (select 1 from game.play_records r where r.auth_user = u.id);
  insert into game.deleted_players (id_hash)
    values (pg_catalog.sha256(pg_catalog.convert_to(delete_player.player::text, 'UTF8')))
    on conflict (id_hash) do update set deleted_at = now();
  return jsonb_build_object('deleted', removed > 0);
end;
$$;
revoke execute on function public.delete_player(uuid) from public, anon, authenticated;
grant execute on function public.delete_player(uuid) to anon, authenticated;
