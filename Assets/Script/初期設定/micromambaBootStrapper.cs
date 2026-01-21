using System.Diagnostics;
using UnityEngine;

public class micromambaBootStrapper : MonoBehaviour
{
    //micromamba.exe install --root-prefix "mamba_root" -p "mamba_root\envs\unitypy" -c conda-forge pandas scikit-learn lightgbm
    public void RunBootstrap()
    {
        // プロジェクトのルートを基準にパスを構成
        string projectPath = Application.dataPath.Replace("/Assets", "");
        string batPath = System.IO.Path.Combine(projectPath, "Tools", "micromamba", "bootstrap.bat");

        // プロセス情報設定
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = false,
            CreateNoWindow = false,  // コンソールを見たい場合は true→false
            WorkingDirectory = System.IO.Path.GetDirectoryName(batPath)
        };

        // 実行
        Process.Start(psi);
        UnityEngine.Debug.Log("[Micromamba] bootstrap.bat executed.");
    }
}
