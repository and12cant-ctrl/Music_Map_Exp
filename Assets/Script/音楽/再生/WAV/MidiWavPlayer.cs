using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(AudioSource))]
public class MidiWavPlayer : MonoBehaviour
{
    [Header("入力ファイル（StreamingAssets配下/絶対パスも可）")]
    public string midiPath = "edited_score.mid";
    public string soundFontPath = "Arachno.sf2";

    [Header("出力キャッシュ（StreamingAssets/Cache 下に出力）")]
    public string outputWavName = "current_render.wav";

    [Header("再生オプション")]
    public float startSeconds = 0f;
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop = true;

    private AudioSource _audio;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
    }

    private async void Start()
    {
#if !UNITY_EDITOR && !UNITY_STANDALONE_WIN
        Debug.LogError("現状は Windows 向けのみ実装。別OS対応は次段で行います。");
        return;
#endif
        _cts = new CancellationTokenSource();
        try
        {
            // 絶対パス解決
            var midiAbs = ResolvePath(midiPath, FluidSynthPaths.ScoresDir);
            var sf2Abs = ResolvePath(soundFontPath, FluidSynthPaths.SoundFontsDir);
            var wavAbs = Path.Combine(FluidSynthPaths.CacheDir, outputWavName);

            // 変換
            Debug.Log($"[FluidSynth] Convert: {midiAbs} -> {wavAbs}");
            await MidiConverter.ConvertMidiToWavAsync(midiAbs, sf2Abs, wavAbs, 60_000, _cts.Token);

            // 再生
            await PlayWavAsync(wavAbs, startSeconds, loop, volume, _cts.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
        }
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_audio != null)
        {
            _audio.Stop();
            _audio.clip = null;
        }
    }

    private static string ResolvePath(string maybeRelative, string baseDirIfRelative)
    {
        // 既に絶対パスならそのまま
        if (Path.IsPathRooted(maybeRelative)) return Path.GetFullPath(maybeRelative);

        // StreamingAssets 起点の相対とみなす
        return Path.GetFullPath(Path.Combine(baseDirIfRelative, maybeRelative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private async Task PlayWavAsync(string wavAbsPath, float startSec, bool loop, float vol, CancellationToken ct)
    {
        // StreamingAssets の file:// URL を生成
        var url = "file://" + wavAbsPath.Replace("\\", "/");

        using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
        var op = req.SendWebRequest();
        while (!op.isDone)
        {
            if (ct.IsCancellationRequested) return;
            await Task.Yield();
        }

#if UNITY_2020_1_OR_NEWER
        if (req.result != UnityWebRequest.Result.Success)
#else
        if (req.isNetworkError || req.isHttpError)
#endif
        {
            throw new Exception($"WAV の読み込みに失敗: {req.error}\n{url}");
        }

        var clip = DownloadHandlerAudioClip.GetContent(req);
        _audio.Stop();
        _audio.clip = clip;
        _audio.loop = loop;
        _audio.volume = vol;
        _audio.time = Mathf.Clamp(startSec, 0f, clip.length - 0.001f);
        _audio.Play();

        Debug.Log($"[Audio] Play: {Path.GetFileName(wavAbsPath)} start={_audio.time:F2}s loop={loop} vol={vol:F2}");
    }
}
