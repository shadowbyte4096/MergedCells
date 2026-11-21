// The library's report email ("Moderation" in docs/systems/server.md). A POST from the library-report-email cron job sends
// the email through Resend; a GET from one of its links decides that level. Every decision is made in the database
// (supabase/migrations/20261121201047_library_report_emails.sql), reached as postgres because the game schema is private.
// Email links carry no sign-in, so verify_jwt is off (supabase/config.toml) and a POST needs the key from Vault instead.
import postgres from 'postgres'

const sql = postgres(Deno.env.get('SUPABASE_DB_URL')!, { prepare: false, max: 1 })
const linksUrl = `${Deno.env.get('SUPABASE_URL')}/functions/v1/library-reports`

function text(status: number, body: string): Response {
  return new Response(body, { status, headers: { 'Content-Type': 'text/plain; charset=utf-8' } })
}

async function decide(req: Request): Promise<Response> {
  const code = new URL(req.url).searchParams.get('t')
  if (!code) {
    return text(404, 'This link is missing its code.')
  }
  const [row] = await sql`select game.report_email_decide(${code}) as reply`
  return text(200, row.reply)
}

async function send(req: Request): Promise<Response> {
  const [check] = await sql`select game.report_email_key_ok(${req.headers.get('x-report-key')}) as ok`
  if (!check.ok) {
    return text(401, 'The report key is wrong.')
  }
  const key = Deno.env.get('RESEND_API_KEY')
  const to = Deno.env.get('REPORT_EMAIL_TO')
  if (!key || !to) {
    console.error(`library-reports: ${key ? 'REPORT_EMAIL_TO' : 'RESEND_API_KEY'} is not set, so nothing was sent`)
    return text(500, 'A secret is missing.')
  }

  const [prepared] = await sql`select game.report_email_prepare(${linksUrl}) as mail`
  const mail = prepared.mail
  if (!mail) {
    return text(200, 'nothing new')
  }
  const resend = await fetch('https://api.resend.com/emails', {
    method: 'POST',
    headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      from: 'Merged Cells <onboarding@resend.dev>',
      to: [to],
      subject: mail.subject,
      html: mail.html,
      text: mail.text,
    }),
  })
  if (!resend.ok) {
    // Not marked sent: the last email's links keep working, and tomorrow's run tries again with fresh ones
    console.error(`library-reports: Resend refused the email (status ${resend.status}): ${await resend.text()}`)
    return text(502, 'Resend refused the email.')
  }
  await sql`select game.report_email_sent(${mail.id})`
  return text(200, 'sent')
}

Deno.serve(async (req) => {
  try {
    if (req.method === 'GET') {
      return await decide(req)
    }
    if (req.method === 'POST') {
      return await send(req)
    }
    // HEAD included: some inboxes check links with it, and it must never decide anything
    return new Response(null, { status: 204 })
  } catch (error) {
    console.error('library-reports:', error instanceof Error ? error.message : error)
    return text(500, 'Something went wrong. Try the link again later.')
  }
})
