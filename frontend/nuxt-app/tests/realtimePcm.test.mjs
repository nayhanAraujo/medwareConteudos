import assert from 'node:assert/strict'
import test from 'node:test'
import { Pcm24Encoder } from '../public/audio/realtime-pcm.js'

for (const rate of [16000, 24000, 44100, 48000, 96000]) {
  test(`resamples ${rate} Hz in 128-sample blocks into PCM16 24kHz/40ms frames`, () => {
    const frames = []
    const encoder = new Pcm24Encoder(rate, frame => frames.push(frame))
    for (let start = 0; start < rate; start += 128) encoder.push([new Float32Array(Math.min(128, rate - start)).fill(0.5)])
    encoder.flush()
    // Floating-point phase accumulation may produce one extra padded frame.
    assert.ok(frames.length >= 25 && frames.length <= 26)
    assert.ok(frames.every(frame => frame.byteLength === 1920))
    assert.ok(Math.abs(new DataView(frames[2]).getInt16(100, true) - 16384) < 10)
  })
}

test('mixes stereo to mono and emits zero-initialized silence', () => {
  const frames = []
  const encoder = new Pcm24Encoder(48000, frame => frames.push(frame))
  encoder.push([new Float32Array(1920).fill(0.5), new Float32Array(1920).fill(-0.5)])
  assert.ok(frames.length >= 1)
  assert.ok(new Uint8Array(frames[0]).every(v => v === 0))
})

test('clamps samples to signed PCM16 and pads the final frame', () => {
  const frames = []
  const encoder = new Pcm24Encoder(24000, frame => frames.push(frame))
  encoder.push([new Float32Array(500).fill(-2)])
  encoder.flush()
  assert.equal(new DataView(frames[0]).getInt16(100, true), -32768)
  assert.equal(new DataView(frames[0]).getInt16(1500, true), 0)
})

test('downsampling attenuates content above the output Nyquist frequency', () => {
  const samples = new Float32Array(48000)
  for (let i = 0; i < samples.length; i++) samples[i] = Math.sin(2 * Math.PI * 18000 * i / 48000)
  let energy = 0, count = 0
  const encoder = new Pcm24Encoder(48000, frame => {
    const view = new DataView(frame)
    for (let i = 100; i < 960; i++) { energy += (view.getInt16(i * 2, true) / 32768) ** 2; count++ }
  })
  encoder.push([samples])
  assert.ok(Math.sqrt(energy / count) < 0.03)
})
