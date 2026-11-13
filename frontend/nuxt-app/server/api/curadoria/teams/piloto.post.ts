import { defineEventHandler, toWebRequest, sendWebResponse } from 'h3'
import { useRuntimeConfig } from '#imports'
import { handleCuradoriaPilot } from '../../../utils/curadoriaPilot'

export default defineEventHandler(async event => sendWebResponse(event,
  await handleCuradoriaPilot(toWebRequest(event), useRuntimeConfig(event).curadoriaPilotChatSha256)))
