# Unity起動失敗の原因報告（ChatGPTへ貼り付け可能）

## イベントシステム拡張後の追記

今回の拡張検証では既存のUnity Editorプロセスが起動していないことを確認し、元プロジェクトを非表示のバッチモードで起動しました。Unity 6000.3.10f1で起動・コンパイル・Play Mode遷移を含む66件のテストが成功し、失敗は0件でした。実際のStartボタン、人物参加、昼夜切替、Hint／Flag付与、履歴、遅延FollowUp、保存復元、サンプルIDの再Importも確認しています。

証拠は `Logs/EventExpansion/verified-results.xml` と `Logs/EventExpansion/verified-unity-tests.log` です。以前の「同じprojectPathを開いたEditorが存在する状態で別Editorを起動した」条件では今回は実行していません。以下は以前の失敗について、再現ログから確定した原因と対処の記録です。

## 結論

Unityそのものが起動不能だったわけではありません。既にUnity Editorが開いている同じプロジェクトへ、テスト用の別Editorをバッチ起動したため、Unityの二重起動防止で終了しました。これは今回のイベント配置コードのコンパイルエラーが原因ではありません。

## 環境と確認事実

- OS: Windows 11
- Unity: 6000.3.10f1
- 元プロジェクト: G:\Unity\Product\Music_Map_Exp
- 作業中のEditorが同じprojectPathで起動していることをプロセス情報で確認。
- 元プロジェクトのTemp/UnityLockfileも存在。
- 同じprojectPathでバッチ起動すると終了コード1を再現。
- 標準出力を保存した再現調査で、次のメッセージを直接取得した。

```text
Aborting batchmode due to fatal error:
別のUnityが起動中で、このプロジェクトを開いているようです。

複数のUnityを起動して同じプロジェクトを開くことはできません。

プロジェクト: G:/Unity/Product/Music_Map_Exp
```

## 最初のログだけでは判断できなかった理由

最初のUnityの-logFile出力には、プロジェクトパス設定の後に「return code 1」で終了したことしか記録されていませんでした。二重起動の具体的理由は標準出力側へ出ていました。標準出力・標準エラーを別ファイルに保存して再現したことで原因を確定しました。

また、最初の制限環境内から起動したプロセスは、ログが生成されず残存していました。この試行単独の停止位置はログがないため断定できません。許可された実行環境で再試行した元プロジェクトへの起動失敗は、上記の二重起動メッセージで確認済みです。今回作成した残存バッチプロセスは終了し、元のEditorとそのインポートワーカーは終了していません。

## 実施した対処

作業中のEditorを強制終了したり、ロックファイルを削除したりせず、元プロジェクトのTemp/EventPlacementValidationへAssets・Packages・ProjectSettings・Libraryをコピーし、別projectPathとして起動しました。

このコピーではUnityの起動・コンパイル・EditModeテスト・Play Modeへの遷移に成功しました。元プロジェクトの未保存Sceneには触れていません。

## 別件1: Play ModeスモークテストのNullReferenceException

Unity起動成功後、検証用コルーチン内で一時的なNullReferenceExceptionが発生しました。ゲーム開始ログは既に出ており、Unity起動失敗とは別問題です。

再生成確認に追加したラムダがローカル変数をキャプチャし、EnterPlayModeによるドメイン再読み込みをまたぐコルーチンのクロージャ状態が関係すると判断しました。検証処理をEnterPlayMode後に呼ぶ通常のメソッドへ移し、コルーチンをシーン開始・yield・検証メソッド呼び出し・終了だけにしたところ、同じゲーム実装のまま解消しました。Unity本体の不具合やゲーム側Rootの欠落が原因だったとは断定していません。

この修正はLogs内と検証コピー内の一時的なテストだけに加えました。

## 別件2: 検証コピー内のMIDIツール不足

Play Mode開始時に、検証コピーのTools/micromamba/micromamba.exeがないというMIDI側のErrorログが出ました。コピーにはToolsを含めていないためです。元プロジェクト側の同ファイルは存在を確認済みです。

これはイベント配置・UIテストとは別の環境差分です。音楽再生を含むゲーム全体が完全に検証済みという意味ではありません。

## 最終確認結果

- Unityコンパイルエラー: なし（既存の警告あり）
- 最終テスト: 46件成功／失敗0
- 内訳: 既存・追加EditModeテスト45件＋Play Modeスモーク1件
- Play Mode: 検証コピーで実際に開始・終了
- 確認内容: 新イベント9件、背景、拠点除外、セルとManagerの対応、新予約UIへの到達、テスト用Spriteでの新アイコン参照、再生成後の旧状態・表示の除去
- nographicsでの自動確認のため、画面の目視・キーボード操作・音楽再生は未検証

## 証拠ファイル

プロジェクト内のLogs/EventPlacementMigrationに保存しています。

- startup-stdout.txt: 二重起動の理由を含む標準出力
- startup-stderr.txt: 今回の再現では空
- startup-probe.log: 二重起動再現時のUnityログ、終了コード1
- unity-tests.log: 最初のバッチ起動時の短いUnityログ
- verified-results.xml: 最終46件成功のテスト結果
- verified-unity-tests.log: 最終実行のUnityログ
- EventPlacementPlaySmoke.cs: 一時的なPlay Mode検証コード

## 今後の実行方法

同じプロジェクトを開いている間は、既存EditorのTest Runnerを使用するか、今回のように別projectPathのコピーでバッチテストを実行します。元プロジェクトでバッチを実行する場合は、利用者が未保存作業を保存して既存Editorを閉じた後に行います。起動中のEditorが存在する状態でUnityLockfileを削除して解決しようとはしません。
