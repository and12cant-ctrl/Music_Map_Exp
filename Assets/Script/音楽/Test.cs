using System.IO;
using UnityEngine;

public class Test : MonoBehaviour
{
    string MusicXML = Path.Combine(FluidSynthPaths.EmotionDir, "UnClass", "Chop-28-2_Intro", "Chop-28-2_INTRO_m1-4_ts4_4_E0.230_S0.875_MAIN.musicxml");
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MusicXMLStart.AnalyzeEmotionFromMusicXML(MusicXML);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
