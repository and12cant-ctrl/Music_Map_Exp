using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public static class MusicXmlMinimalParser
{
    // MusicXMLの divisions は「1拍を何分割しているか」の単位になる
    // duration/divisions = 四分音符(=1拍)換算
    public static List<ScoreEvent> Parse(string xmlText, Part targetPart, voice targetVoice)
    {
        var partVoiceToPart = new Dictionary<(string partId, int voiceNo), Part>();

        var doc = XDocument.Parse(xmlText);

        // namespace 対策（MusicXMLはns付きが多い）
        XNamespace ns = doc.Root?.Name.Namespace ?? "";
        var partEls = doc.Descendants(ns + "part").ToList();
        if (partEls.Count == 0) throw new Exception("MusicXML: <part> が見つかりません");

        var results = new List<ScoreEvent>();

        for (int partIndex = 0; partIndex < partEls.Count; partIndex++)
        {
            var partEl = partEls[partIndex];
            string partId = partEl.Attribute("id")?.Value ?? $"P{partIndex}";

            // とりあえず 0→Part.A, 1→Part.B に割り当て（2パート想定）
            Part partForThisXmlPart = (partIndex == 0) ? Part.A : Part.B;

            int divisions = 1;
            float beatsPerBar = 4f;

            foreach (var measureEl in partEl.Elements(ns + "measure"))
            {
                int bar = int.TryParse(measureEl.Attribute("number")?.Value, out var m) ? m : (results.Count + 1);

                var attr = measureEl.Element(ns + "attributes");
                if (attr != null)
                {
                    var divEl = attr.Element(ns + "divisions");
                    if (divEl != null && int.TryParse(divEl.Value, out var d) && d > 0) divisions = d;

                    var timeEl = attr.Element(ns + "time");
                    if (timeEl != null)
                    {
                        var beatsEl = timeEl.Element(ns + "beats");
                        var beatTypeEl = timeEl.Element(ns + "beat-type");
                        if (beatsEl != null && beatTypeEl != null &&
                            int.TryParse(beatsEl.Value, out var beats) &&
                            int.TryParse(beatTypeEl.Value, out var beatType))
                        {
                            beatsPerBar = beats * (4f / beatType);
                        }
                    }
                }

                //foreach (var noteEl in measureEl.Elements(ns + "note"))
                //{
                //    bool isRest = noteEl.Element(ns + "rest") != null;
                //    bool isChord = noteEl.Element(ns + "chord") != null;

                //    var durEl = noteEl.Element(ns + "duration");
                //    if (durEl == null) continue;
                //    if (!float.TryParse(durEl.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var durDiv))
                //        continue;

                //    float durBeats = durDiv / divisions;
                //    float start = cursorBeats;
                //    int voiceNumber = 1;
                //    var voiceEl = noteEl.Element(ns + "voice");
                //    if (voiceEl != null && int.TryParse(voiceEl.Value, out var vn) && vn > 0)
                //        voiceNumber = vn;

                //    // ★ここで「voiceが違うなら別Part」を実現
                //    Part partForThisNote = GetOrAssignPart(partVoiceToPart, partId, voiceNumber);

                //    string pitch = "REST";
                //    if (!isRest)
                //    {
                //        var pitchEl = noteEl.Element(ns + "pitch");
                //        if (pitchEl != null)
                //        {
                //            var step = pitchEl.Element(ns + "step")?.Value;
                //            var alter = pitchEl.Element(ns + "alter")?.Value;
                //            var octave = pitchEl.Element(ns + "octave")?.Value;

                //            if (!string.IsNullOrEmpty(step) && !string.IsNullOrEmpty(octave))
                //            {
                //                string accidental = "";
                //                if (alter == "1") accidental = "#";
                //                else if (alter == "-1") accidental = "♭";
                //                pitch = $"{step}{accidental}{octave}";
                //            }
                //        }
                //    }

                //    results.Add(new ScoreEvent
                //    {
                //        part = partForThisXmlPart,
                //        voice = voice.Up,     // ← あとで同時刻3音制限の割り当てをするなら仮
                //        pitch = pitch,
                //        duration = durBeats,
                //        bar = bar,
                //        start = start
                //    });

                //    if (!isChord) cursorBeats += durBeats;
                //}
                float cursorBeats = 0f;

                foreach (var child in measureEl.Elements())
                {
                    if (child.Name == ns + "note")
                    {
                        var noteEl = child;

                        bool isRest = noteEl.Element(ns + "rest") != null;
                        bool isChord = noteEl.Element(ns + "chord") != null;

                        var durEl = noteEl.Element(ns + "duration");
                        if (durEl == null) continue;

                        float durDiv = float.Parse(durEl.Value, CultureInfo.InvariantCulture);
                        float durBeats = durDiv / divisions;

                        float start = cursorBeats;

                        // pitch処理（省略）
                        string pitch = "REST";
                        results.Add(new ScoreEvent
                        {
                            part = partForThisXmlPart,
                            voice = targetVoice,
                            pitch = pitch,
                            duration = durBeats,
                            bar = bar,
                            start = start
                        });

                        // chord の場合は時間進めない
                        if (!isChord)
                            cursorBeats += durBeats;
                    }

                    else if (child.Name == ns + "backup")
                    {
                        var durEl = child.Element(ns + "duration");
                        float durDiv = float.Parse(durEl.Value, CultureInfo.InvariantCulture);
                        cursorBeats -= durDiv / divisions;
                    }

                    else if (child.Name == ns + "forward")
                    {
                        var durEl = child.Element(ns + "duration");
                        float durDiv = float.Parse(durEl.Value, CultureInfo.InvariantCulture);
                        cursorBeats += durDiv / divisions;
                    }
                }
                #region MyRegion
                //int minMidi = int.MaxValue, maxMidi = int.MinValue;
                //string minPitch = null, maxPitch = null;

                //foreach (var e in results)
                //{
                //    if (e.pitch == "REST") continue;
                //    int mi = StaffCoordinateConverter.PitchStringToMidi(e.pitch); // 既存の変換を使う
                //    if (mi < minMidi) { minMidi = mi; minPitch = e.pitch; }
                //    if (mi > maxMidi) { maxMidi = mi; maxPitch = e.pitch; }
                //}

                //Debug.Log($"[ParsedRange] min={minPitch}, max={maxPitch}, count={results.Count}");
                #endregion

            }
        }

        return results;
    }

    public static Part GetOrAssignPart(
    Dictionary<(string partId, int voiceNo), Part> map,
    string partId,
    int voiceNo)
    {
        var key = (partId, voiceNo);
        if (map.TryGetValue(key, out var p)) return p;

        // まだ未割当なら、次の空きを割り当て
        // 4つを超える場合は、とりあえずAにまとめる（後で方針を決めてもOK）
        int n = map.Count;
        Part assigned = n switch
        {
            0 => Part.A,
            1 => Part.B,
            2 => Part.C,
            3 => Part.D,
            _ => Part.A
        };

        map[key] = assigned;
        return assigned;
    }
}

