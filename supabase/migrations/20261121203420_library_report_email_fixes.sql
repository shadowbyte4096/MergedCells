-- The report email's final review (20261121201047_library_report_emails.sql otherwise unchanged):
-- * Keep never clears reports, or approves an edit, that came after its email: Keep ran library_restore, which deletes all
--   of a level's reports, so an old email's Keep could unhide a level on reports the developer never saw. It now changes
--   nothing and says so, and an edit of a reported level also makes the next email, so that email shows the level as it is.
-- * Each link sits on a line of its own: side by side they made small, close tap targets for choices that can't be undone.
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
  ) and not exists (
    select 1 from game.library_review v join game.library_levels l on l.id = v.id where l.updated_at > last_sent
  ) then
    return null;
  end if;

  select count(*), count(*) filter (where v.hidden_at is not null) into total, hidden from game.library_review v;
  insert into game.report_emails default values returning report_emails.id into mail_id;

  for lvl in
    select v.*, l.updated_at from game.library_review v join game.library_levels l on l.id = v.id
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
         || case when lvl.last_report > last_sent then ', new' else '' end
         || case when lvl.updated_at > last_sent then ', edited' else '' end;
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
         || '<p class="link"><a href="' || game.report_html(keep_url) || '">Keep</a></p>'
         || '<p class="link"><a href="' || game.report_html(take_down_url) || '">Take down</a></p>'
         || '<p class="link"><a href="' || game.report_html(stop_url) || '">Take down and stop this player</a></p></div>';
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
       || 'body{font-family:sans-serif;line-height:1.4}.level{margin:0 0 32px}.tags{font-weight:bold}'
       || '.code{font-family:monospace;word-break:break-all}.link{margin:0 0 18px;font-size:1.15em}.quiet{color:#666}'
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
  edited timestamptz;
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
  select l.name, l.removed_at, l.updated_at into level_name, removed, edited
  from game.library_levels l where l.id = link_level for update;
  select k.action into link_action from game.report_links k where k.code = report_email_decide.code;
  if not found then
    return gone;
  end if;
  if removed is not null then
    delete from game.report_links k where k.level = link_level;
    return 'This level is already gone.';
  end if;
  level_name := game.report_name(level_name);

  -- library_restore deletes every report, so Keep would also clear ones the email never showed. The links stay: taking it
  -- down is still the developer's call, and the next email shows the level as it is now
  if link_action = 'keep' and (edited > prepared or exists (
    select 1 from game.library_reports r where r.level = link_level and r.reported_at > prepared
  )) then
    return format('Nothing changed: "%s" was reported again or edited after this email went out. The next email shows it as it is now.',
                  level_name);
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

  return case link_action
    when 'keep' then format('Kept "%s". Its reports are cleared, and new reports can hide it again.', level_name)
    when 'take_down' then format('Took down "%s" for good.', level_name)
    else format('Took down "%s" and stopped its author publishing.', level_name)
  end;
end;
$$;
revoke execute on function game.report_email_decide(text) from public, anon, authenticated;
