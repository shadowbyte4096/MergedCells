-- Chunk 34b's final review: player IDs aren't tied to sign-ins (chunk 33's model), so one anonymous sign-in could report a
-- level under three made-up player IDs and hide it. A level now hides once reports from 3 different sign-ins hold it;
-- honest players each have their own sign-in, so nothing changes for them. library_report is otherwise as in
-- 20261121120000_library.sql.
create or replace function public.library_report(player uuid, level uuid, reason text)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  caller uuid := auth.uid();
  author uuid;
  removed timestamptz;
begin
  if caller is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if library_report.player is null or library_report.level is null or library_report.reason is null
      or library_report.reason not in ('name', 'picture', 'other') then
    raise exception 'A player, a level and a reason (name, picture or other) are needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_report.player::text, 0));
  if game.is_deleted(library_report.player) then
    return jsonb_build_object('reported', false);
  end if;
  select l.player, l.removed_at into author, removed from game.library_levels l where l.id = library_report.level;
  if not found or removed is not null or author = library_report.player then
    return jsonb_build_object('reported', false);
  end if;

  insert into game.players (id) values (library_report.player)
    on conflict (id) do update set last_seen = now();
  insert into game.library_reports (level, player, reason, auth_user)
    values (library_report.level, library_report.player, library_report.reason, caller)
    on conflict (level, player) do nothing;
  if (select count(distinct r.auth_user) from game.library_reports r where r.level = library_report.level) >= 3 then
    update game.library_levels l set hidden_at = now() where l.id = library_report.level and l.hidden_at is null;
  end if;
  return jsonb_build_object('reported', true);
end;
$$;
revoke execute on function public.library_report(uuid, uuid, text) from public, anon, authenticated;
grant execute on function public.library_report(uuid, uuid, text) to authenticated;
