import Foundation
import AppKit
import Vision

// RPT-APPROVER-INTEGRITY-01 — استخراج النصّ ومواضعه من لقطات الشاشة (قراءة فقط).
// الإخراج: TSV إلى stdout — path \t x \t y \t w \t h \t text  (بكسل، أصل أعلى-يسار)

let args = Array(CommandLine.arguments.dropFirst())
if args.isEmpty { FileHandle.standardError.write("usage: ocr.swift <img>...\n".data(using: .utf8)!); exit(2) }

for path in args {
    guard let img = NSImage(contentsOfFile: path),
          let cg = img.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
        FileHandle.standardError.write("SKIP \(path)\n".data(using: .utf8)!); continue
    }
    let W = CGFloat(cg.width), H = CGFloat(cg.height)
    let req = VNRecognizeTextRequest()
    req.recognitionLevel = .accurate
    req.usesLanguageCorrection = false
    req.recognitionLanguages = ["ar-SA", "ar", "en-US"]
    let handler = VNImageRequestHandler(cgImage: cg, options: [:])
    do { try handler.perform([req]) } catch {
        FileHandle.standardError.write("FAIL \(path) \(error)\n".data(using: .utf8)!); continue
    }
    guard let obs = req.results else { continue }
    var out = ""
    for o in obs {
        guard let top = o.topCandidates(1).first else { continue }
        let s = top.string
        // صندوق لكلّ كلمة على حدة كي يقتصر التمويه على الاسم لا على السطر كلّه
        var i = s.startIndex
        while i < s.endIndex {
            while i < s.endIndex, s[i] == " " { i = s.index(after: i) }
            if i >= s.endIndex { break }
            var j = i
            while j < s.endIndex, s[j] != " " { j = s.index(after: j) }
            let word = String(s[i..<j]).replacingOccurrences(of: "\t", with: " ")
            if let box = try? top.boundingBox(for: i..<j) {
                let bb = box.boundingBox
                let x = Int(bb.minX * W), w = Int(bb.width * W)
                let y = Int((1 - bb.maxY) * H), h = Int(bb.height * H)
                out += "\(path)\t\(x)\t\(y)\t\(w)\t\(h)\t\(word)\n"
            }
            i = j
        }
    }
    FileHandle.standardOutput.write(out.data(using: .utf8)!)
}
