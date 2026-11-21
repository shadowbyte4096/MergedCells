-- Checks the library's report email (supabase/migrations/20261121201047_library_report_emails.sql) on the live project, in
-- one transaction that rolls back, so nothing is kept and nothing is sent. Makes its own players, levels and reports. The
-- only real rows it touches are the report emails, cleared first (and back after the rollback) so a real email sent in the
-- last hour can't fail the checks. Exit 1 lists the failed checks; otherwise the result reads "<N> checks passed".
--   supabase db query --remote --yes --file supabase/tests/library_report_emails.sql
begin;

create temp table checks (n serial, name text, ok boolean);
create function pg_temp.check(text, boolean) returns void language sql as $$
  insert into checks (name, ok) values ($1, coalesce($2, false))
$$;
delete from game.report_emails;

do $$
declare
  base constant text := 'https://example.test/functions/v1/library-reports';
  gone constant text := 'This link no longer works: it was used, a newer email replaced it, or it''s over 14 days old.';
  author uuid := gen_random_uuid();
  author2 uuid := gen_random_uuid();
  readers uuid[] := array[gen_random_uuid(), gen_random_uuid(), gen_random_uuid()];
  signins uuid[] := array[gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), gen_random_uuid()];
  a uuid := gen_random_uuid();
  b uuid := gen_random_uuid();
  c uuid := gen_random_uuid();
  d uuid := gen_random_uuid();
  f uuid := gen_random_uuid();
  g uuid := gen_random_uuid();
  h_level uuid := gen_random_uuid();
  j_level uuid := gen_random_uuid();
  unchanged constant text := 'Nothing changed: "%s" was reported again or edited after this email went out. The next email shows it as it is now.';
  hostile constant text := '<b>&"x''';
  mail jsonb;
  first_mail uuid;
  second_mail uuid;
  third_mail uuid;
  email_id uuid;
  level_id uuid;
  code text;
  reply text;
  everyone text[];
  leaked boolean;
begin
  insert into game.players (id) select unnest(array[author, author2] || readers);
  -- Every test level was published and last edited 3 days ago, so only the checks that move updated_at make an edit
  insert into game.library_levels (id, player, code, name, layout, rows, columns, par, score, auth_user, published_at, updated_at, hidden_at)
  values (a, author, 'MC1-test', 'Report check A', '0123456789abcdef', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days', null),
         (b, author, 'MC1-test', hostile, '0123456789abcdee', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days', now() - interval '1 hour'),
         (c, author2, 'MC1-test', 'Report check C', '0123456789abcded', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days', null);
  insert into game.library_reports (level, player, reason, auth_user, reported_at)
  values (a, readers[1], 'name', signins[1], now() - interval '1 hour'),
         (b, readers[1], 'picture', signins[1], now() - interval '1 hour'),
         (b, readers[2], 'picture', signins[2], now() - interval '1 hour'),
         (b, readers[3], 'other', signins[3], now() - interval '1 hour'),
         (c, readers[1], 'other', signins[1], now() - interval '1 hour');

  insert into game.report_emails (prepared_at, sent_at) values (now() + interval '1 day', now()) returning report_emails.id into email_id;
  perform pg_temp.check('nothing new gives no email', game.report_email_prepare(base) is null);
  delete from game.report_emails e where e.id = email_id;

  insert into game.report_emails (prepared_at, sent_at) values (now() - interval '2 hours', now() - interval '2 hours');
  mail := game.report_email_prepare(base);
  perform pg_temp.check('a new report gives an email',
    mail ? 'id' and mail ? 'subject' and mail ? 'html' and mail ? 'text');
  perform pg_temp.check('the subject counts levels and hidden ones',
    mail ->> 'subject' ~ '^Merged Cells: \d+ reported levels? \(\d+ hidden\)$');
  perform pg_temp.check('hidden levels come first',
    position('&lt;b&gt;&amp;&quot;x&#39;' in mail ->> 'html') between 1 and position('Report check A' in mail ->> 'html') - 1);
  perform pg_temp.check('a level name is escaped',
    position('&lt;b&gt;&amp;&quot;x&#39;' in mail ->> 'html') > 0 and position(hostile in mail ->> 'html') = 0);
  everyone := array[author, author2]::text[] || readers::text[] || signins::text[];
  select bool_or(position(x in mail ->> 'html') > 0 or position(x in mail ->> 'text') > 0) into leaked from unnest(everyone) x;
  perform pg_temp.check('no player ID or sign-in is in the email', not leaked);
  perform pg_temp.check('each link is on a line of its own', (mail ->> 'html') !~ '</a>\s*<a');
  first_mail := (mail ->> 'id')::uuid;
  perform pg_temp.check('each listed level has three links',
    (select array_agg(k.action order by k.action) from game.report_links k where k.email = first_mail and k.level = a)
      = array['keep', 'stop', 'take_down']);
  perform game.report_email_sent(first_mail);

  reply := game.report_email_decide((select k.code from game.report_links k where k.level = b and k.action = 'keep'));
  perform pg_temp.check('keep clears reports and unhides',
    reply = format('Kept "%s". Its reports are cleared, and new reports can hide it again.', hostile)
      and (select l.hidden_at is null from game.library_levels l where l.id = b)
      and not exists (select 1 from game.library_reports r where r.level = b));
  perform pg_temp.check('a level''s links work once', not exists (select 1 from game.report_links k where k.level = b));

  code := (select k.code from game.report_links k where k.level = a and k.action = 'take_down');
  reply := game.report_email_decide(code);
  perform pg_temp.check('take down removes the level',
    reply = 'Took down "Report check A" for good.'
      and (select l.removed_at is not null from game.library_levels l where l.id = a));
  perform pg_temp.check('a used link does nothing', game.report_email_decide(code) = gone);

  reply := game.report_email_decide((select k.code from game.report_links k where k.level = c and k.action = 'stop'));
  perform pg_temp.check('stop also stops the author',
    reply = 'Took down "Report check C" and stopped its author publishing.'
      and exists (select 1 from game.stopped_publishers s where s.player = author2)
      and (select l.removed_at is not null from game.library_levels l where l.id = c));

  perform pg_temp.check('an unknown code does nothing', game.report_email_decide(md5(random()::text)) = gone);

  -- prepare() stamps now(), fixed for the transaction, so later reports need the earlier emails moved back in time
  update game.report_emails e set prepared_at = now() - interval '1 hour' where e.id = first_mail;
  insert into game.library_levels (id, player, code, name, layout, rows, columns, par, score, auth_user, published_at, updated_at)
  values (d, author, 'MC1-test', 'Report check D', '0123456789abcdec', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days'),
         (f, author, 'MC1-test', 'Report check F', '0123456789abcdeb', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days');
  insert into game.library_reports (level, player, reason, auth_user, reported_at)
  values (d, readers[1], 'name', signins[1], now() - interval '40 minutes'),
         (f, readers[1], 'name', signins[1], now() - interval '40 minutes');
  second_mail := (game.report_email_prepare(base) ->> 'id')::uuid;
  perform game.report_email_sent(second_mail);
  update game.report_emails e set prepared_at = now() - interval '30 minutes' where e.id = second_mail;
  insert into game.library_reports (level, player, reason, auth_user, reported_at)
  values (f, readers[2], 'name', signins[2], now() - interval '10 minutes');
  mail := game.report_email_prepare(base);
  perform pg_temp.check('a report between prepare and sent is new next time', mail is not null);
  third_mail := (mail ->> 'id')::uuid;

  reply := game.report_email_decide(
    (select k.code from game.report_links k where k.email = second_mail and k.level = d and k.action = 'keep'));
  perform pg_temp.check('an unsent email leaves the last email''s links working', reply like 'Kept "Report check D".%');

  code := (select k.code from game.report_links k where k.email = second_mail and k.level = f and k.action = 'keep');
  perform game.report_email_sent(third_mail);
  perform pg_temp.check('sending a newer email kills older links', game.report_email_decide(code) = gone);

  update game.report_emails e set prepared_at = now() - interval '15 days' where e.id = third_mail;
  code := (select k.code from game.report_links k where k.email = third_mail and k.level = f and k.action = 'keep');
  perform pg_temp.check('an expired link does nothing', code is not null and game.report_email_decide(code) = gone);

  insert into game.library_levels (id, player, code, name, layout, rows, columns, par, score, auth_user, published_at, updated_at)
  values (g, author, 'MC1-test', 'Report check G', '0123456789abcdea', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days');
  insert into game.library_reports (level, player, reason, auth_user, reported_at)
  values (g, readers[1], 'name', signins[1], now() - interval '5 minutes');
  email_id := (game.report_email_prepare(base) ->> 'id')::uuid;
  perform game.report_email_sent(email_id);
  code := (select k.code from game.report_links k where k.email = email_id and k.level = g and k.action = 'keep');
  perform game.library_take_down(g);
  perform pg_temp.check('a removed level is already gone', game.report_email_decide(code) = 'This level is already gone.');

  -- Keep must never clear reports, or approve an edit, that came after its email
  update game.report_emails e set prepared_at = now() - interval '1 hour' where e.id = email_id;
  insert into game.library_levels (id, player, code, name, layout, rows, columns, par, score, auth_user, published_at, updated_at)
  values (h_level, author, 'MC1-test', 'Report check H', '0123456789abcde9', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days'),
         (j_level, author, 'MC1-test', 'Report check J', '0123456789abcde8', 5, 5, 3, 50, signins[4], now() - interval '3 days', now() - interval '3 days');
  insert into game.library_reports (level, player, reason, auth_user, reported_at)
  values (h_level, readers[1], 'name', signins[1], now() - interval '50 minutes'),
         (j_level, readers[1], 'name', signins[1], now() - interval '50 minutes');
  email_id := (game.report_email_prepare(base) ->> 'id')::uuid;
  perform game.report_email_sent(email_id);
  -- After every earlier test report (F's latest is 10 minutes old), so only J's edit can make the next email
  update game.report_emails e set prepared_at = now() - interval '5 minutes' where e.id = email_id;
  perform pg_temp.check('an unchanged reported level gives no email', game.report_email_prepare(base) is null);
  update game.library_levels l set updated_at = now() - interval '2 minutes' where l.id = j_level;
  perform pg_temp.check('an edit of a reported level gives an email', game.report_email_prepare(base) is not null);
  insert into game.library_reports (level, player, reason, auth_user, reported_at)
  values (h_level, readers[2], 'picture', signins[2], now() - interval '2 minutes');
  reply := game.report_email_decide(
    (select k.code from game.report_links k where k.email = email_id and k.level = h_level and k.action = 'keep'));
  perform pg_temp.check('keep does nothing after a new report',
    reply = format(unchanged, 'Report check H') and (select count(*) from game.library_reports r where r.level = h_level) = 2);
  reply := game.report_email_decide(
    (select k.code from game.report_links k where k.email = email_id and k.level = j_level and k.action = 'keep'));
  perform pg_temp.check('keep does nothing after an edit',
    reply = format(unchanged, 'Report check J') and exists (select 1 from game.library_reports r where r.level = j_level));
  reply := game.report_email_decide(
    (select k.code from game.report_links k where k.email = email_id and k.level = h_level and k.action = 'take_down'));
  perform pg_temp.check('take down still works after a new report', reply = 'Took down "Report check H" for good.');

  update game.report_emails e set prepared_at = now() - interval '2 minutes' where e.id = email_id;
  for i in 1..51 loop
    level_id := gen_random_uuid();
    insert into game.library_levels (id, player, code, name, layout, rows, columns, par, score, auth_user, published_at, updated_at, hidden_at)
    values (level_id, author, 'MC1-test', 'Report check many ' || i, lpad(to_hex(i), 16, 'f'), 5, 5, 3, 50, signins[4],
            now() - interval '3 days', now() - interval '3 days', now() - interval '1 minute');
    insert into game.library_reports (level, player, reason, auth_user, reported_at)
    values (level_id, readers[1], 'name', signins[1], now() - interval '1 minute');
  end loop;
  mail := game.report_email_prepare(base);
  perform pg_temp.check('at most 50 levels are listed',
    (select count(*) from game.report_links k where k.email = (mail ->> 'id')::uuid) <= 150
      and position('more: see game.library_review' in mail ->> 'html') > 0);

  perform pg_temp.check('the key check refuses a wrong key', not game.report_email_key_ok('wrong'));
  perform pg_temp.check('the key check accepts the Vault key', game.report_email_key_ok(
    (select s.decrypted_secret from vault.decrypted_secrets s where s.name = 'library_report_email_key')));
  perform pg_temp.check('the request queues a call', game.report_email_request() is not null);
  perform pg_temp.check('the job is scheduled',
    exists (select 1 from cron.job j where j.jobname = 'library-report-email' and j.schedule = '0 8 * * *'));
end;
$$;

do $$
begin
  if exists (select 1 from checks where not ok) then
    raise exception 'FAIL: %', (select string_agg(name, '; ' order by n) from checks where not ok);
  end if;
end;
$$;

select count(*) || ' checks passed' as result from checks;
rollback;
