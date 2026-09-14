import { defineEventHandler } from 'h3'
import { useRuntimeConfig } from '#imports'
import { handleDocsHttp } from '../../../../utils/docsHttp.ts'

export default defineEventHandler(event => handleDocsHttp(event, 'openapi', useRuntimeConfig(event)))
