// Stateful polyphase low-pass resampler. No browser globals: also exercised by Node tests.
export class Pcm24Encoder {
  constructor(inputRate, emit) {
    if (!Number.isFinite(inputRate) || inputRate < 8000 || inputRate > 192000) throw new Error('Invalid sample rate')
    this.emit = emit
    this.step = inputRate / 24000
    this.next = 0
    this.count = -1
    this.ring = new Float32Array(64)
    this.frame = new ArrayBuffer(1920)
    this.view = new DataView(this.frame)
    this.offset = 0
    const cutoff = Math.min(0.45, 0.45 / this.step)
    this.filters = Array.from({ length: 128 }, (_, phase) => {
      const coefficients = Array.from({ length: 32 }, (_, i) => {
        const distance = 15 - i + phase / 128
        const sinc = Math.abs(distance) < 1e-9 ? 2 * cutoff : Math.sin(2 * Math.PI * cutoff * distance) / (Math.PI * distance)
        return sinc * (0.5 + 0.5 * Math.cos(Math.PI * distance / 17))
      })
      const sum = coefficients.reduce((a, b) => a + b, 0)
      return coefficients.map(x => x / sum)
    })
  }

  push(channels) {
    if (!channels.length || !channels[0]?.length) return
    for (let j = 0; j < channels[0].length; j++) {
      this.count++
      let input = 0
      for (const channel of channels) input += channel[j] || 0
      this.ring[this.count % 64] = input / channels.length
      while (this.next <= this.count) {
        const phase = Math.min(127, Math.max(0, Math.floor((this.count - this.next) * 128)))
        const filter = this.filters[phase]
        let output = 0
        for (let i = 0; i < 32; i++) if (this.count >= i) output += this.ring[(this.count - i) % 64] * filter[i]
        const value = Math.max(-1, Math.min(1, output))
        this.view.setInt16(this.offset * 2, Math.round(value * (value < 0 ? 32768 : 32767)), true)
        this.offset++
        if (this.offset === 960) this.send()
        this.next += this.step
      }
    }
  }

  send() {
    const frame = this.frame
    this.frame = new ArrayBuffer(1920)
    this.view = new DataView(this.frame)
    this.offset = 0
    this.emit(frame)
  }

  flush() { if (this.offset > 0) this.send() } // Remaining PCM bytes are already zero-filled.
}
