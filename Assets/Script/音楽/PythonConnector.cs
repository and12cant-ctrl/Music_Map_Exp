//using System.Diagnostics;
//using UnityEngine;
//using System.IO;

//public class PythonConnector : MonoBehaviour
//{
//    public string pythonPath = @"C:\Python39\python.exe"; // é¿çsä¬ã´Ç…çáÇÌÇπÇƒí≤êÆ
//    public string scriptPath = @"C:\MyProject\judge_music.py";
//    public string midiPathPlayer = @"C:\MyProject\player.mid";
//    public string midiPathAnswer = @"C:\MyProject\answer.mid";

//    public void RunMusicJudge()
//    {
//        ProcessStartInfo psi = new ProcessStartInfo
//        {
//            FileName = pythonPath,
//            Arguments = $"{scriptPath} {midiPathPlayer} {midiPathAnswer}",
//            UseShellExecute = false,
//            RedirectStandardOutput = true,
//            CreateNoWindow = true
//        };

//        Process process = new Process { StartInfo = psi };
//        process.Start();

//        string output = process.StandardOutput.ReadToEnd();
//        process.WaitForExit();

//    }
//}
