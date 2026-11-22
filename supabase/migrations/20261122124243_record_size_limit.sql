-- After chunk 34b: a record over 131072 bytes (measured as jsonb text) is skipped, down from 1 MB. The game sends none
-- over ServerConfig.MaxRecordBytes (65536), half this, since jsonb's text spaces out every key and value; with infinite
-- maps capped at 30×30 an honest record is about 8 KB. upload_records is otherwise as in 20261120120000_leaderboard_spread.sql.
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
          or octet_length(entry::text) > 131072 then
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
