-- After chunk 34b: a daily email to the developer listing every reported library level, with one-time links to keep it,
-- take it down, or take it down and stop its author ("Moderation" in docs/systems/server.md). The Edge Function
-- library-reports sends it through Resend and answers the links. Everything it decides lives here, granted to nobody: the
-- function connects as postgres through SUPABASE_DB_URL. Checked by supabase/tests/library_report_emails.sql.
create extension if not exists pg_net with schema extensions;

-- Only the newest sent email's links work: report_email_sent deletes the rest
create table game.report_emails (
  id uuid primary key default gen_random_uuid(),
  prepared_at timestamptz not null default now(),
  sent_at timestamptz
);

create table game.report_links (
  code text primary key,
  email uuid not null references game.report_emails (id) on delete cascade,
  level uuid not null references game.library_levels (id) on delete cascade,
  action text not null check (action in ('keep', 'take_down', 'stop'))
);
create index report_links_email on game.report_links (email);
create index report_links_level on game.report_links (level);

alter table game.report_emails enable row level security;
alter table game.report_links enable row level security;
revoke all on game.report_emails from public, anon, authenticated;
revoke all on game.report_links from public, anon, authenticated;

-- A player's level name as email text: control characters become spaces, so a name can't fake a line of its own
create or replace function game.report_name(t text)
returns text
language sql
immutable
set search_path = ''
as $$
  select pg_catalog.regexp_replace(t, '[[:cntrl:]]', ' ', 'g');
$$;
revoke execute on function game.report_name(text) from public, anon, authenticated;

create or replace function game.report_html(t text)
returns text
language sql
immutable
set search_path = ''
as $$
  select replace(replace(replace(replace(replace(t, '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), '"', '&quot;'), '''', '&#39;');
$$;
revoke execute on function game.report_html(text) from public, anon, authenticated;

-- The next email, or null when no report came in after the newest sent email was prepared. Lists up to 50 levels from
-- game.library_review, hidden first, then the most reported, each with three new links (links_url?t=<code>)
create or replace function game.report_email_prepare(links_url text)
returns jsonb
language plpgsql
set search_path = ''
as $$
declare
  last_sent timestamptz;
  mail_id uuid;
  total integer;
  hidden integer;
  shown integer := 0;
  lvl record;
  keep_url text;
  take_down_url text;
  stop_url text;
  live_levels bigint;
  shown_name text;
  tags text;
  facts text[];
  html text := '';
  plain text := '';
  intro text;
  outro text := 'Each link works once, only from the newest email, for 14 days.';
begin
  select max(e.prepared_at) into last_sent from game.report_emails e where e.sent_at is not null;
  last_sent := coalesce(last_sent, '-infinity');
  if not exists (
    select 1 from game.library_reports r join game.library_levels l on l.id = r.level
    where l.removed_at is null and r.reported_at > last_sent
  ) then
    return null;
  end if;

  select count(*), count(*) filter (where v.hidden_at is not null) into total, hidden from game.library_review v;
  insert into game.report_emails default values returning report_emails.id into mail_id;

  for lvl in
    select v.* from game.library_review v
    order by v.hidden_at is null, v.name_reports + v.picture_reports + v.other_reports desc, v.last_report desc nulls last
    limit 50
  loop
    shown := shown + 1;
    keep_url := encode(extensions.gen_random_bytes(16), 'hex');
    take_down_url := encode(extensions.gen_random_bytes(16), 'hex');
    stop_url := encode(extensions.gen_random_bytes(16), 'hex');
    insert into game.report_links (code, email, level, action)
    values (keep_url, mail_id, lvl.id, 'keep'), (take_down_url, mail_id, lvl.id, 'take_down'), (stop_url, mail_id, lvl.id, 'stop');
    keep_url := links_url || '?t=' || keep_url;
    take_down_url := links_url || '?t=' || take_down_url;
    stop_url := links_url || '?t=' || stop_url;

    select count(*) into live_levels from game.library_levels l where l.player = lvl.author and l.removed_at is null;
    shown_name := game.report_name(lvl.name);
    tags := case when lvl.hidden_at is not null then 'Hidden' else 'Live' end
         || case when lvl.last_report > last_sent then ', new' else '' end;
    facts := array[format('Reports: %s name, %s picture, %s other', lvl.name_reports, lvl.picture_reports, lvl.other_reports)]
          || case when lvl.last_report is null then array[]::text[]
                  else array['Last report ' || to_char(lvl.last_report at time zone 'UTC', 'YYYY-MM-DD')] end
          || array['Published ' || to_char(lvl.published_at at time zone 'UTC', 'YYYY-MM-DD'),
                   format('Its author has %s level%s in the library', live_levels, case when live_levels = 1 then '' else 's' end),
                   'Level ID ' || lvl.id];

    html := html || '<div class="level"><h2>' || game.report_html(shown_name) || '</h2>'
         || '<p class="tags">' || tags || '</p>'
         || '<p>' || array_to_string(array(select game.report_html(f) from unnest(facts) f), '<br>') || '</p>'
         || '<p class="code">' || game.report_html(lvl.code) || '</p>'
         || '<p class="links"><a href="' || game.report_html(keep_url) || '">Keep</a>'
         || '<a href="' || game.report_html(take_down_url) || '">Take down</a>'
         || '<a href="' || game.report_html(stop_url) || '">Take down and stop this player</a></p></div>';
    plain := plain || shown_name || E'\n' || tags || E'\n' || array_to_string(facts, E'\n') || E'\n' || lvl.code || E'\n'
          || 'Keep: ' || keep_url || E'\n'
          || 'Take down: ' || take_down_url || E'\n'
          || 'Take down and stop this player: ' || stop_url || E'\n\n';
  end loop;

  intro := format('%s reported level%s, %s hidden. Paste a level''s code into the game''s Import to see it.',
                  total, case when total = 1 then '' else 's' end, hidden);
  if total > shown then
    outro := format('And %s more: see game.library_review in the SQL editor. ', total - shown) || outro;
  end if;
  html := '<!DOCTYPE html><html><head><meta charset="utf-8"><style>'
       || 'body{font-family:sans-serif;line-height:1.4}.level{margin:0 0 28px}.tags{font-weight:bold}'
       || '.code{font-family:monospace;word-break:break-all}.links a{margin-right:18px}.quiet{color:#666}'
       || '</style></head><body><p>' || intro || '</p>' || html || '<p class="quiet">' || outro || '</p></body></html>';
  plain := intro || E'\n\n' || plain || outro || E'\n';

  return jsonb_build_object(
    'id', mail_id,
    'subject', format('Merged Cells: %s reported level%s (%s hidden)', total, case when total = 1 then '' else 's' end, hidden),
    'html', html,
    'text', plain);
end;
$$;
revoke execute on function game.report_email_prepare(text) from public, anon, authenticated;

-- Called once Resend has the email: earlier emails' links stop working
create or replace function game.report_email_sent(email uuid)
returns void
language plpgsql
set search_path = ''
as $$
begin
  update game.report_emails e set sent_at = now() where e.id = report_email_sent.email;
  if found then
    delete from game.report_emails e where e.id <> report_email_sent.email;
  end if;
end;
$$;
revoke execute on function game.report_email_sent(uuid) from public, anon, authenticated;

-- An opened link: carries out its choice once, and returns the line the function shows
create or replace function game.report_email_decide(code text)
returns text
language plpgsql
set search_path = ''
as $$
declare
  gone constant text := 'This link no longer works: it was used, a newer email replaced it, or it''s over 14 days old.';
  link_level uuid;
  link_action text;
  prepared timestamptz;
  level_name text;
  removed timestamptz;
begin
  select k.level, e.prepared_at into link_level, prepared
  from game.report_links k join game.report_emails e on e.id = k.email
  where k.code = report_email_decide.code;
  if not found then
    return gone;
  end if;
  if prepared <= now() - interval '14 days' then
    delete from game.report_links k where k.code = report_email_decide.code;
    return gone;
  end if;

  -- Two links for one level opened together: the second waits here, then finds its link deleted
  select l.name, l.removed_at into level_name, removed from game.library_levels l where l.id = link_level for update;
  select k.action into link_action from game.report_links k where k.code = report_email_decide.code;
  if not found then
    return gone;
  end if;
  if removed is not null then
    delete from game.report_links k where k.level = link_level;
    return 'This level is already gone.';
  end if;

  if link_action = 'keep' then
    perform game.library_restore(link_level);
  elsif link_action = 'take_down' then
    perform game.library_take_down(link_level);
  else
    perform game.library_stop(link_level);
    perform game.library_take_down(link_level);
  end if;
  delete from game.report_links k where k.level = link_level;

  level_name := game.report_name(level_name);
  return case link_action
    when 'keep' then format('Kept "%s". Its reports are cleared, and new reports can hide it again.', level_name)
    when 'take_down' then format('Took down "%s" for good.', level_name)
    else format('Took down "%s" and stopped its author publishing.', level_name)
  end;
end;
$$;
revoke execute on function game.report_email_decide(text) from public, anon, authenticated;

-- Compares digests, so the time a comparison takes says nothing usable about the key
create or replace function game.report_email_key_ok(key text)
returns boolean
language sql
stable
set search_path = ''
as $$
  select coalesce((
    select extensions.digest(report_email_key_ok.key, 'sha256') = extensions.digest(s.decrypted_secret, 'sha256')
    from vault.decrypted_secrets s where s.name = 'library_report_email_key'
  ), false);
$$;
revoke execute on function game.report_email_key_ok(text) from public, anon, authenticated;

-- The cron job's call, and the way to send the email by hand: select game.report_email_request();
create or replace function game.report_email_request()
returns bigint
language sql
set search_path = ''
as $$
  select net.http_post(
    url := 'https://jdbfwviusxlfhcqhpowr.supabase.co/functions/v1/library-reports',
    body := '{}'::jsonb,
    headers := jsonb_build_object(
      'Content-Type', 'application/json',
      'x-report-key', (select s.decrypted_secret from vault.decrypted_secrets s where s.name = 'library_report_email_key')),
    timeout_milliseconds := 10000);
$$;
revoke execute on function game.report_email_request() from public, anon, authenticated;

-- Made in the database, so its value is never in a file
select vault.create_secret(encode(extensions.gen_random_bytes(32), 'hex'), 'library_report_email_key',
                           'Sent by the library-report-email cron job to the library-reports Edge Function');

select cron.schedule('library-report-email', '0 8 * * *', 'select game.report_email_request()');
