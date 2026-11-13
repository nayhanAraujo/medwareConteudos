import { defineEventHandler, getRequestURL, removeResponseHeader, setResponseHeader } from 'h3'
import { isTeamsCuradoriaPath } from '../../utils/curadoriaPublicRoutes'

// Restricted to these two pages, not /curadoria/**, API responses or the rest of the site.
export default defineEventHandler(event => {
  if (!isTeamsCuradoriaPath(getRequestURL(event).pathname)) return
  setResponseHeader(event, 'Content-Security-Policy', "frame-ancestors 'self' https://teams.microsoft.com https://*.teams.microsoft.com https://*.cloud.microsoft https://*.microsoft365.com https://*.office.com;")
  removeResponseHeader(event, 'X-Frame-Options')
  setResponseHeader(event, 'Cache-Control', 'no-store')
})
