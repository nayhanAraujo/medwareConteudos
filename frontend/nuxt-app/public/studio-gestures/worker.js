// The MediaPipe WASM loader uses importScripts in a classic worker.
let landmarker

self.onmessage = async ({ data }) => {
  try {
    if (data.type === 'start') {
      const { FilesetResolver, HandLandmarker } = await import(data.runtime + '/vision_bundle.mjs')
      const files = await FilesetResolver.forVisionTasks(data.runtime + '/wasm')
      landmarker = await HandLandmarker.createFromOptions(files, {
        baseOptions: { modelAssetPath: data.model },
        runningMode: 'VIDEO',
        numHands: 1
      })
      self.postMessage({ type: 'ready' })
      return
    }

    const { frame, timestamp } = data
    try {
      const result = landmarker.detectForVideo(frame, timestamp)
      self.postMessage({ type: 'frame', hand: result.landmarks[0] ?? null, timestamp, aspect: frame.width / frame.height })
    } finally {
      frame.close()
    }
  } catch {
    self.postMessage({ type: 'error' })
  }
}
