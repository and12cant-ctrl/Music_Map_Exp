using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class EmptyScore : MonoBehaviour
{
    [System.Serializable]
    public struct NoteToken //1音、休符の最小単位
    {
        public bool isResr;
        public int midi;
        public float qlen;
        public int velocity;
    }

    [System.Serializable]
    public class MemberScore
    {
        public string title = "Empty Part";
        public string timeSignature = "4/4";
        public int tempoBPM = 60;

        //ここが空なら空の楽譜
        public List<NoteToken> _tokens = new List<NoteToken>();
    }

    //空のパート譜を作るヘルパー
    public static MemberScore NewEmptyPart(
        string title = "Empty Part",
        string timeSig = "4/4",
        int bpm = 60)
    {
        return new MemberScore
        { 
            title = title,
            timeSignature = timeSig,
            tempoBPM = bpm,
            _tokens = new List<NoteToken>()
        };
    }
}
public enum signature
{

}
