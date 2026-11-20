-- Chunk 33: leaderboard scores and play-record uploads (see docs/systems/server.md).
-- The tables sit in the private game schema, which the Data API never exposes. The game reaches them only through the
-- three functions in public below, each security definer with its own grant. Their caps (500 scores, 100 records, 1 MB a
-- record) sit above the game's batch numbers in config/game_config.json's server group.

create schema if not exists game;
revoke all on schema game from public, anon, authenticated;

-- The game's own random player ID: every row hangs off it, so one delete finds everything
create table game.players (
  id uuid primary key,
  first_seen timestamptz not null default now(),
  last_seen timestamptz not null default now()
);

-- Every shipped level's layout and par, from supabase/levels.sql; rows are never deleted
create table game.levels (
  id integer not null check (id >= 1),
  layout text not null check (layout ~ '^[0-9a-f]{16}$'),
  par integer not null check (par >= 1),
  primary key (id, layout)
);

-- The lowest moves per player, level and layout. auth_user is the anonymous sign-in that last sent it, for removing abuse
create table game.scores (
  player uuid not null references game.players (id) on delete cascade,
  level integer not null,
  layout text not null,
  moves integer not null check (moves >= 1),
  auth_user uuid not null,
  updated_at timestamptz not null default now(),
  primary key (player, level, layout),
  foreign key (level, layout) references game.levels (id, layout)
);
create index scores_level_layout on game.scores (level, layout);
create index scores_auth_user on game.scores (auth_user);

-- Each record is the file's line as sent; started is when it was recorded, which retention reads
create table game.play_records (
  id uuid primary key,
  player uuid not null references game.players (id) on delete cascade,
  started timestamptz not null,
  record jsonb not null,
  auth_user uuid not null,
  received_at timestamptz not null default now()
);
create index play_records_player on game.play_records (player);
create index play_records_started on game.play_records (started);
create index play_records_auth_user on game.play_records (auth_user);

-- A deleted ID's SHA-256, never the ID, kept 7 days: an upload that timed out on the phone can still land after its
-- deletion, and must not bring the player back
create table game.deleted_players (
  id_hash bytea primary key,
  deleted_at timestamptz not null default now()
);

alter table game.players enable row level security;
alter table game.levels enable row level security;
alter table game.scores enable row level security;
alter table game.play_records enable row level security;
alter table game.deleted_players enable row level security;

create or replace function game.is_deleted(player uuid)
returns boolean
language sql
stable
set search_path = ''
as $$
  select exists (
    select 1 from game.deleted_players d
    where d.id_hash = pg_catalog.sha256(pg_catalog.convert_to(player::text, 'UTF8')));
$$;
revoke execute on function game.is_deleted(uuid) from public, anon, authenticated;

-- Keeps each score that names a listed level and isn't below its par, as the lowest so far
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

-- Stores each record under its own GUID, once. A bad record is skipped, never raised, so it can't block later uploads
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

-- "Delete my data": everything under the ID, and the anonymous sign-ins that sent it, whose sessions keep IP addresses.
-- Callable without a session, because the game drops its session before asking; the random ID is the bearer
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

-- Retention, daily: records two years after they were recorded, players silent for two years with everything under
-- them, anonymous sign-ins with nothing left after 30 days, and deleted IDs' hashes after 7 days
create or replace function game.purge()
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
  delete from game.play_records r where r.started < now() - interval '2 years';
  delete from game.players p where p.last_seen < now() - interval '2 years';
  delete from auth.users u
    where u.is_anonymous
      and u.created_at < now() - interval '30 days'
      and not exists (select 1 from game.scores s where s.auth_user = u.id)
      and not exists (select 1 from game.play_records r where r.auth_user = u.id);
  delete from game.deleted_players d where d.deleted_at < now() - interval '7 days';
end;
$$;
revoke execute on function game.purge() from public, anon, authenticated;

create extension if not exists pg_cron with schema pg_catalog;
select cron.schedule('game-purge', '17 3 * * *', 'select game.purge()');
