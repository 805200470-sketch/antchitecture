// join_videos.swift <out.mp4> <in.mp4>...  -  joins clips of one format end to end without re-encoding (AVFoundation
// passthrough), e.g. the nests' 21-second walk films into one reel to loop on a screen. The films already fade in from
// black and out to black, so the joins and the loop point need no transition. Run: swift Tools/join_videos.swift ...
import AVFoundation

let args = CommandLine.arguments
guard args.count >= 3 else { print("usage: swift join_videos.swift <out.mp4> <in.mp4>..."); exit(1) }
let out = URL(fileURLWithPath: args[1])
if FileManager.default.fileExists(atPath: out.path) { print("join_videos: \(out.path) exists, give a new name"); exit(1) }

let comp = AVMutableComposition()
let track = comp.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
var at = CMTime.zero
for p in args.dropFirst(2) {
    let asset = AVURLAsset(url: URL(fileURLWithPath: p))
    guard let src = try await asset.loadTracks(withMediaType: .video).first else { print("join_videos: no video in \(p)"); exit(1) }
    let range = try await src.load(.timeRange)
    try track.insertTimeRange(range, of: src, at: at)
    at = at + range.duration
    print(String(format: "join_videos: %@ %.2f s", (p as NSString).lastPathComponent, range.duration.seconds))
}

let export = AVAssetExportSession(asset: comp, presetName: AVAssetExportPresetPassthrough)!
export.shouldOptimizeForNetworkUse = true   // index at the front, so it starts playing at once
try await export.export(to: out, as: .mp4)
print(String(format: "join_videos: %@ %.2f s", out.path, at.seconds))
