import { Pcm24Encoder } from './realtime-pcm.js'

class StudioRealtimeProcessor extends AudioWorkletProcessor {
  constructor() {
    super()
    this.encoder = new Pcm24Encoder(sampleRate, frame => this.port.postMessage(frame, [frame]))
    this.flushed = false
    this.port.onmessage = event => {
      if (event.data === 'flush') {
        this.flushed = true
        this.encoder.flush()
        this.port.postMessage('flushed')
      }
    }
  }
  process(inputs) {
    if (!this.flushed) this.encoder.push(inputs[0] || [])
    return true
  }
}
registerProcessor('studio-realtime-pcm', StudioRealtimeProcessor)
