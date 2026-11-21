-- Chunk 34b: the online library of players' custom levels (see docs/systems/server.md).
-- The tables sit in the private game schema and are reached only through the functions in public below, each security
-- definer with its own grant. Every row hangs off game.players on delete cascade, so delete_player and the two-year purge
-- reach it. The server never reads inside a level's share code: every phone re-checks it. The numbers the server holds: a
-- level hides at 3 different reporters, reports are kept 90 days at most, 100 library rows a player, 50 rows a page, codes
-- of at most 4,096 characters (LibraryConfig.MaxCodeCharacters and MaxPageSize mirror the last two).

-- One row per published level, keyed by its GUID. A tombstone (removed_at set) keeps only the ID and author, so a removed
-- level can't come back under the same ID. auth_user is the anonymous sign-in that last sent it, for removing abuse
create table game.library_levels (
  id uuid primary key,
  player uuid not null references game.players (id) on delete cascade,
  code text check (code ~ '^MC1-[A-Za-z0-9_-]+$' and char_length(code) <= 4096),
  name text check (octet_length(name) between 1 and 512),
  layout text check (layout ~ '^[0-9a-f]{16}$'),
  rows integer not null check (rows between 1 and 255),
  columns integer not null check (columns between 1 and 255),
  par integer not null check (par >= 1),
  score integer not null check (score between 0 and 100),
  auth_user uuid not null,
  published_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  hidden_at timestamptz,
  reviewed_at timestamptz,
  removed_at timestamptz,
  check ((removed_at is null) = (code is not null and name is not null and layout is not null)),
  check (removed_at is null or hidden_at is null)
);
create index library_levels_player on game.library_levels (player);
create index library_levels_published_at on game.library_levels (published_at);
create index library_levels_layout on game.library_levels (layout);
create index library_levels_auth_user on game.library_levels (auth_user);

-- Each player who beat a level, once; "most played" counts them
create table game.library_finishes (
  level uuid not null references game.library_levels (id) on delete cascade,
  player uuid not null references game.players (id) on delete cascade,
  auth_user uuid not null,
  finished_at timestamptz not null default now(),
  primary key (level, player)
);
create index library_finishes_player on game.library_finishes (player);
create index library_finishes_auth_user on game.library_finishes (auth_user);

create table game.library_likes (
  level uuid not null references game.library_levels (id) on delete cascade,
  player uuid not null references game.players (id) on delete cascade,
  auth_user uuid not null,
  liked_at timestamptz not null default now(),
  primary key (level, player)
);
create index library_likes_player on game.library_likes (player);
create index library_likes_auth_user on game.library_likes (auth_user);

-- One report per player and level, deleted when the developer reviews the level and after 90 days at most
create table game.library_reports (
  level uuid not null references game.library_levels (id) on delete cascade,
  player uuid not null references game.players (id) on delete cascade,
  reason text not null check (reason in ('name', 'picture', 'other')),
  auth_user uuid not null,
  reported_at timestamptz not null default now(),
  primary key (level, player)
);
create index library_reports_player on game.library_reports (player);
create index library_reports_auth_user on game.library_reports (auth_user);
create index library_reports_reported_at on game.library_reports (reported_at);

-- A player who blocked whoever published a level; the author's ID never leaves the server
create table game.library_blocks (
  player uuid not null references game.players (id) on delete cascade,
  author uuid not null references game.players (id) on delete cascade,
  blocked_at timestamptz not null default now(),
  primary key (player, author)
);
create index library_blocks_author on game.library_blocks (author);

-- Players the developer stopped publishing. On the player ID, so "Delete my data" escapes it: there are no accounts
create table game.stopped_publishers (
  player uuid primary key references game.players (id) on delete cascade,
  stopped_at timestamptz not null default now()
);

alter table game.library_levels enable row level security;
alter table game.library_finishes enable row level security;
alter table game.library_likes enable row level security;
alter table game.library_reports enable row level security;
alter table game.library_blocks enable row level security;
alter table game.stopped_publishers enable row level security;

-- A level's counts as the caller sees them: never who finished or liked it
create or replace function game.library_counts(level uuid, player uuid, author uuid)
returns jsonb
language sql
stable
set search_path = ''
as $$
  select jsonb_build_object(
    'listed', true,
    'mine', library_counts.author = library_counts.player,
    'finished', (select count(*) from game.library_finishes f where f.level = library_counts.level),
    'likes', (select count(*) from game.library_likes k where k.level = library_counts.level),
    'liked', exists (select 1 from game.library_likes k where k.level = library_counts.level and k.player = library_counts.player));
$$;
revoke execute on function game.library_counts(uuid, uuid, uuid) from public, anon, authenticated;

-- Publishes a level, or replaces the caller's earlier version of it. An update keeps when it was first published (so
-- Newest doesn't reward re-publishing), its counts and whether it's hidden
create or replace function public.library_publish(
  player uuid, level uuid, code text, name text, layout text, rows integer, columns integer, par integer, score integer)
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
  held boolean;
begin
  if caller is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if library_publish.player is null or library_publish.level is null
      or library_publish.code is null or library_publish.code !~ '^MC1-[A-Za-z0-9_-]+$' or char_length(library_publish.code) > 4096
      or library_publish.name is null or octet_length(library_publish.name) not between 1 and 512
      or library_publish.layout is null or library_publish.layout !~ '^[0-9a-f]{16}$'
      or library_publish.rows is null or library_publish.rows not between 1 and 255
      or library_publish.columns is null or library_publish.columns not between 1 and 255
      or library_publish.par is null or library_publish.par < 1
      or library_publish.score is null or library_publish.score not between 0 and 100 then
    raise exception 'A level''s code, name, layout, size, par and score are needed, each in range.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_publish.player::text, 0));
  if game.is_deleted(library_publish.player) then
    return jsonb_build_object('outcome', 'deleted');
  end if;

  insert into game.players (id) values (library_publish.player)
    on conflict (id) do update set last_seen = now();
  if exists (select 1 from game.stopped_publishers s where s.player = library_publish.player) then
    return jsonb_build_object('outcome', 'stopped');
  end if;
  select l.player, l.removed_at into author, removed from game.library_levels l where l.id = library_publish.level;
  held := found;
  if held and author <> library_publish.player then
    return jsonb_build_object('outcome', 'taken');
  end if;
  if held and removed is not null then
    return jsonb_build_object('outcome', 'removed');
  end if;
  if exists (select 1 from game.levels g where g.layout = library_publish.layout) then
    return jsonb_build_object('outcome', 'campaign');
  end if;
  if exists (select 1 from game.library_levels l where l.layout = library_publish.layout and l.player <> library_publish.player) then
    return jsonb_build_object('outcome', 'copy');
  end if;
  if not held and (select count(*) from game.library_levels l where l.player = library_publish.player) >= 100 then
    return jsonb_build_object('outcome', 'full');
  end if;

  if held then
    update game.library_levels l
      set code = library_publish.code, name = library_publish.name, layout = library_publish.layout, rows = library_publish.rows,
          columns = library_publish.columns, par = library_publish.par, score = library_publish.score, auth_user = caller,
          updated_at = now()
      where l.id = library_publish.level;
  else
    insert into game.library_levels (id, player, code, name, layout, rows, columns, par, score, auth_user)
      values (library_publish.level, library_publish.player, library_publish.code, library_publish.name, library_publish.layout,
              library_publish.rows, library_publish.columns, library_publish.par, library_publish.score, caller);
  end if;
  return jsonb_build_object('outcome', 'published');
end;
$$;
revoke execute on function public.library_publish(uuid, uuid, text, text, text, integer, integer, integer, integer) from public, anon, authenticated;
grant execute on function public.library_publish(uuid, uuid, text, text, text, integer, integer, integer, integer) to authenticated;

-- The author takes a level out. A hidden one leaves a tombstone, so it can't be republished at once, unhidden and with no
-- reports; any other is deleted with its finishes, likes and reports. Callable without a session, like delete_player
create or replace function public.library_remove(player uuid, level uuid)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  removed integer;
begin
  if library_remove.player is null or library_remove.level is null then
    raise exception 'A player and a level are needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_remove.player::text, 0));
  update game.library_levels l
    set code = null, name = null, layout = null, hidden_at = null, removed_at = now()
    where l.id = library_remove.level and l.player = library_remove.player and l.removed_at is null and l.hidden_at is not null;
  get diagnostics removed = row_count;
  if removed > 0 then
    delete from game.library_finishes f where f.level = library_remove.level;
    delete from game.library_likes k where k.level = library_remove.level;
    delete from game.library_reports r where r.level = library_remove.level;
  else
    delete from game.library_levels l
      where l.id = library_remove.level and l.player = library_remove.player and l.removed_at is null;
    get diagnostics removed = row_count;
  end if;
  return jsonb_build_object('removed', removed > 0);
end;
$$;
revoke execute on function public.library_remove(uuid, uuid) from public, anon, authenticated;
grant execute on function public.library_remove(uuid, uuid) to anon, authenticated;

-- One page of levels in the chosen order (newest, played, liked), filtered by the publisher's score and the larger side,
-- leaving out hidden levels, tombstones and blocked authors' levels; or, for "mine", every level the caller published,
-- hidden and removed ones included. Never an author's ID, or anyone's finishes, likes, reports or blocks
create or replace function public.library_levels(
  player uuid, view text, min_score integer, max_score integer, min_side integer, max_side integer, page integer, size integer)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  caller uuid := auth.uid();
  per_page integer;
  listed jsonb;
  blocked integer;
begin
  if caller is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if library_levels.player is null or library_levels.view is null or library_levels.view not in ('newest', 'played', 'liked', 'mine')
      or library_levels.page is null or library_levels.page < 0 or library_levels.size is null or library_levels.size < 1 then
    raise exception 'A player, a view, a page and a size are needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_levels.player::text, 0));
  if game.is_deleted(library_levels.player) then
    return jsonb_build_object('levels', '[]'::jsonb, 'blocked', 0, 'playerDeleted', true);
  end if;

  insert into game.players (id) values (library_levels.player)
    on conflict (id) do update set last_seen = now();
  per_page := least(library_levels.size, 50);
  select count(*) into blocked from game.library_blocks b where b.player = library_levels.player;
  with counted as (
    select l.id, l.player as author, l.code, l.name, l.layout, l.rows, l.columns, l.par, l.score, l.published_at, l.hidden_at,
           l.removed_at,
           (select count(*) from game.library_finishes f where f.level = l.id)::integer as finished,
           (select count(*) from game.library_likes k where k.level = l.id)::integer as likes
    from game.library_levels l
    where case when library_levels.view = 'mine' then l.player = library_levels.player
          else l.removed_at is null and l.hidden_at is null
            and not exists (select 1 from game.library_blocks b where b.player = library_levels.player and b.author = l.player)
            and (library_levels.min_score is null or l.score >= library_levels.min_score)
            and (library_levels.max_score is null or l.score <= library_levels.max_score)
            and (library_levels.min_side is null or greatest(l.rows, l.columns) >= library_levels.min_side)
            and (library_levels.max_side is null or greatest(l.rows, l.columns) <= library_levels.max_side)
          end),
  ordered as (
    select c.*, row_number() over (
             order by case library_levels.view when 'played' then c.finished when 'liked' then c.likes else 0 end desc,
                      c.published_at desc, c.id) as place
    from counted c)
  select coalesce(jsonb_agg(jsonb_build_object(
           'id', o.id, 'code', o.code, 'name', o.name, 'layout', o.layout, 'rows', o.rows, 'columns', o.columns, 'par', o.par,
           'score', o.score, 'finished', o.finished, 'likes', o.likes, 'mine', o.author = library_levels.player,
           'hidden', o.hidden_at is not null, 'removed', o.removed_at is not null) order by o.place), '[]'::jsonb)
    into listed
    from ordered o
    where library_levels.view = 'mine'
       or (o.place > library_levels.page * per_page and o.place <= (library_levels.page + 1) * per_page);
  return jsonb_build_object('levels', listed, 'blocked', blocked);
end;
$$;
revoke execute on function public.library_levels(uuid, text, integer, integer, integer, integer, integer, integer) from public, anon, authenticated;
grant execute on function public.library_levels(uuid, text, integer, integer, integer, integer, integer, integer) to authenticated;

-- A win of a listed level, counted once per player and never for its author; answers with the level's counts
create or replace function public.library_finish(player uuid, level uuid)
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
  if library_finish.player is null or library_finish.level is null then
    raise exception 'A player and a level are needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_finish.player::text, 0));
  if game.is_deleted(library_finish.player) then
    return jsonb_build_object('listed', false);
  end if;
  select l.player, l.removed_at into author, removed from game.library_levels l where l.id = library_finish.level;
  if not found or removed is not null then
    return jsonb_build_object('listed', false);
  end if;

  insert into game.players (id) values (library_finish.player)
    on conflict (id) do update set last_seen = now();
  if author <> library_finish.player then
    insert into game.library_finishes (level, player, auth_user) values (library_finish.level, library_finish.player, caller)
      on conflict (level, player) do nothing;
  end if;
  return game.library_counts(library_finish.level, library_finish.player, author);
end;
$$;
revoke execute on function public.library_finish(uuid, uuid) from public, anon, authenticated;
grant execute on function public.library_finish(uuid, uuid) to authenticated;

-- Likes or unlikes a listed level. A like needs the caller's finish, and never counts on their own level
create or replace function public.library_like(player uuid, level uuid, liked boolean)
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
  if library_like.player is null or library_like.level is null or library_like.liked is null then
    raise exception 'A player, a level and whether it''s liked are needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_like.player::text, 0));
  if game.is_deleted(library_like.player) then
    return jsonb_build_object('listed', false);
  end if;
  select l.player, l.removed_at into author, removed from game.library_levels l where l.id = library_like.level;
  if not found or removed is not null then
    return jsonb_build_object('listed', false);
  end if;

  insert into game.players (id) values (library_like.player)
    on conflict (id) do update set last_seen = now();
  if not library_like.liked then
    delete from game.library_likes k where k.level = library_like.level and k.player = library_like.player;
  elsif author <> library_like.player
      and exists (select 1 from game.library_finishes f where f.level = library_like.level and f.player = library_like.player) then
    insert into game.library_likes (level, player, auth_user) values (library_like.level, library_like.player, caller)
      on conflict (level, player) do nothing;
  end if;
  return game.library_counts(library_like.level, library_like.player, author);
end;
$$;
revoke execute on function public.library_like(uuid, uuid, boolean) from public, anon, authenticated;
grant execute on function public.library_like(uuid, uuid, boolean) to authenticated;

-- One report per player and level, never on the caller's own. The third player's report hides the level until the
-- developer reviews it
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
  if (select count(*) from game.library_reports r where r.level = library_report.level) >= 3 then
    update game.library_levels l set hidden_at = now() where l.id = library_report.level and l.hidden_at is null;
  end if;
  return jsonb_build_object('reported', true);
end;
$$;
revoke execute on function public.library_report(uuid, uuid, text) from public, anon, authenticated;
grant execute on function public.library_report(uuid, uuid, text) to authenticated;

-- Blocks whoever published the level, for the caller only; answers with how many players the caller has blocked
create or replace function public.library_block(player uuid, level uuid)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
declare
  caller uuid := auth.uid();
  author uuid;
  blocked integer;
begin
  if caller is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if library_block.player is null or library_block.level is null then
    raise exception 'A player and a level are needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_block.player::text, 0));
  if game.is_deleted(library_block.player) then
    return jsonb_build_object('blocked', 0);
  end if;

  insert into game.players (id) values (library_block.player)
    on conflict (id) do update set last_seen = now();
  select l.player into author from game.library_levels l where l.id = library_block.level;
  if found and author <> library_block.player then
    insert into game.library_blocks (player, author) values (library_block.player, author)
      on conflict (player, author) do nothing;
  end if;
  select count(*) into blocked from game.library_blocks b where b.player = library_block.player;
  return jsonb_build_object('blocked', blocked);
end;
$$;
revoke execute on function public.library_block(uuid, uuid) from public, anon, authenticated;
grant execute on function public.library_block(uuid, uuid) to authenticated;

create or replace function public.library_unblock(player uuid)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
#variable_conflict use_column
begin
  if auth.uid() is null then
    raise exception 'Sign in first.' using errcode = '42501';
  end if;
  if library_unblock.player is null then
    raise exception 'A player is needed.' using errcode = '22023';
  end if;
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(library_unblock.player::text, 0));
  delete from game.library_blocks b where b.player = library_unblock.player;
  return jsonb_build_object('blocked', 0);
end;
$$;
revoke execute on function public.library_unblock(uuid) from public, anon, authenticated;
grant execute on function public.library_unblock(uuid) to authenticated;

-- Moderation, run by the developer in the dashboard's SQL editor and granted to nobody. Every level with reports or
-- hidden, hidden first, then the most reported. Paste a code into the game's Import to see the level
create or replace view game.library_review with (security_invoker = true) as
  select l.id, l.name, l.code, l.player as author,
         (select count(*) from game.library_levels a where a.player = l.player) as author_levels,
         l.published_at, l.hidden_at, l.reviewed_at,
         count(r.level) filter (where r.reason = 'name') as name_reports,
         count(r.level) filter (where r.reason = 'picture') as picture_reports,
         count(r.level) filter (where r.reason = 'other') as other_reports,
         max(r.reported_at) as last_report
  from game.library_levels l
  left join game.library_reports r on r.level = l.id
  where l.removed_at is null
  group by l.id
  having l.hidden_at is not null or count(r.level) > 0
  order by l.hidden_at is null, count(r.level) desc;
revoke all on game.library_review from public, anon, authenticated;

-- The level stays (or comes back): its reports go, and it can be hidden again only by new reports
create or replace function game.library_restore(level uuid)
returns void
language sql
set search_path = ''
as $$
  delete from game.library_reports r where r.level = library_restore.level;
  update game.library_levels l set hidden_at = null, reviewed_at = now() where l.id = library_restore.level;
$$;
revoke execute on function game.library_restore(uuid) from public, anon, authenticated;

-- Takes the level down for good: a tombstone keeps its ID from coming back
create or replace function game.library_take_down(level uuid)
returns void
language sql
set search_path = ''
as $$
  delete from game.library_reports r where r.level = library_take_down.level;
  delete from game.library_finishes f where f.level = library_take_down.level;
  delete from game.library_likes k where k.level = library_take_down.level;
  update game.library_levels l
    set code = null, name = null, layout = null, hidden_at = null, reviewed_at = now(), removed_at = now()
    where l.id = library_take_down.level;
$$;
revoke execute on function game.library_take_down(uuid) from public, anon, authenticated;

-- Stops whoever published the level from publishing again; their levels stay until taken down
create or replace function game.library_stop(level uuid)
returns void
language sql
set search_path = ''
as $$
  insert into game.stopped_publishers (player)
    select l.player from game.library_levels l where l.id = library_stop.level
    on conflict (player) do nothing;
$$;
revoke execute on function game.library_stop(uuid) from public, anon, authenticated;

-- delete_player as in 20261120120000_leaderboard_spread.sql, with the library's tables in both sign-in checks: a sign-in
-- that still holds library rows keeps them, and one that held only this player's rows goes with its IP
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
    select r.auth_user from game.play_records r where r.player = delete_player.player
    union
    select l.auth_user from game.library_levels l where l.player = delete_player.player
    union
    select f.auth_user from game.library_finishes f where f.player = delete_player.player
    union
    select k.auth_user from game.library_likes k where k.player = delete_player.player
    union
    select p.auth_user from game.library_reports p where p.player = delete_player.player) sent;
  delete from game.players p where p.id = delete_player.player;
  get diagnostics removed = row_count;
  delete from auth.users u
    where u.id = any(coalesce(senders, '{}'::uuid[]))
      and u.is_anonymous
      and not exists (select 1 from game.scores s where s.auth_user = u.id)
      and not exists (select 1 from game.play_records r where r.auth_user = u.id)
      and not exists (select 1 from game.library_levels l where l.auth_user = u.id)
      and not exists (select 1 from game.library_finishes f where f.auth_user = u.id)
      and not exists (select 1 from game.library_likes k where k.auth_user = u.id)
      and not exists (select 1 from game.library_reports p where p.auth_user = u.id);
  insert into game.deleted_players (id_hash)
    values (pg_catalog.sha256(pg_catalog.convert_to(delete_player.player::text, 'UTF8')))
    on conflict (id_hash) do update set deleted_at = now();
  return jsonb_build_object('deleted', removed > 0);
end;
$$;
revoke execute on function public.delete_player(uuid) from public, anon, authenticated;
grant execute on function public.delete_player(uuid) to anon, authenticated;

-- game.purge() as in 20261119120000_leaderboards.sql, with reports kept 90 days at most and the library's tables in the
-- check for sign-ins that hold nothing
create or replace function game.purge()
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
  delete from game.play_records r where r.started < now() - interval '2 years';
  delete from game.players p where p.last_seen < now() - interval '2 years';
  delete from game.library_reports r where r.reported_at < now() - interval '90 days';
  delete from auth.users u
    where u.is_anonymous
      and u.created_at < now() - interval '30 days'
      and not exists (select 1 from game.scores s where s.auth_user = u.id)
      and not exists (select 1 from game.play_records r where r.auth_user = u.id)
      and not exists (select 1 from game.library_levels l where l.auth_user = u.id)
      and not exists (select 1 from game.library_finishes f where f.auth_user = u.id)
      and not exists (select 1 from game.library_likes k where k.auth_user = u.id)
      and not exists (select 1 from game.library_reports p where p.auth_user = u.id);
  delete from game.deleted_players d where d.deleted_at < now() - interval '7 days';
end;
$$;
revoke execute on function game.purge() from public, anon, authenticated;
