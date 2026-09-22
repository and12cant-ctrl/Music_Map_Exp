# イベント初期配置の差し替え報告

確認日: 2026-09-14（JST）

## 結果

ゲーム開始時の配置経路を既存の EventMapPlacement に統一した。旧型・旧アセット・旧配置のコードは保持し、実行時の呼び出しを停止した。検証用プロジェクトで最終46ケースが成功した（既存・追加EditModeテスト45件＋一時的なPlay Modeスモークテスト1件）。

```text
GameManager.Start
  → MapGenerator.MapGenerate
      → 背景候補準備
      → EventSystemRoot.PrepareNewMap（登録状態・セル・旧表示の初期化）
      → EnsureGrid／全セルの背景設定
      → EventMapPlacement.Generate
          → 出現可能定義取得
          → 必須イベントの座標割り当て・Spawn
          → ブロック順シャッフル
          → ブロックごとの定義の重み付き抽選
          → 定義に合うセルをランダム選択・Spawn
      → 再生成時の表示更新
  → 初回MiniMapBuilder.SetupMap
```

## 1. 修正ファイル

| ファイル（プロジェクトルートからの相対表記） | 変更 |
|---|---|
| Assets/Script/マップ/Map成形/MapGenerator.cs | 新配置を唯一の実行入口に変更。背景を独立して設定。旧処理を呼ばれないprivateメソッドに保持 |
| Assets/Script/Events/Integration/EventMapPlacement.cs | 必須配置、使用済み座標、重み付き抽選、ブロック・端数処理を更新 |
| Assets/Script/Events/Integration/EventSystemRoot.cs | 拠点予約設定と新規生成用初期化を追加。既存Restoreを使用 |
| Assets/Script/マップ/Map成形/MiniMapBuilder.cs | 再生成用表示リセット、再購読重複防止、テクスチャ幅・高さの修正 |
| Assets/Script/マップ/Map成形/MapIconPresenter.cs | 旧アイコン群の消去を追加 |
| Assets/Script/UI/イベント/派遣/HakenTab.cs | 再生成後の現在セル表示更新を追加 |
| Assets/Script/Events/Editor/Tests/EventSystemTests.cs | 背景設定分離に合わせて既存テストの呼び出しを更新 |
| Assets/Scenes/メインバトル.unity | MainにEventSystemRootを1つ追加し、既存カタログ・UIを設定 |

GameManager、MapDataSO、MapCell、MoveReader、EventManager、MapEventController、EventRunner、予約・判定・効果処理、旧ScriptableObjectには今回変更を加えていない。作業開始時点の未コミット変更は保持した。

## 2. 新規ファイル

- Assets/Script/Events/Editor/Tests/EventPlacementTests.cs
- Assets/Script/Events/Editor/Tests/EventPlacementTests.cs.meta
- Docs/event-placement-migration.md
- Docs/unity-startup-diagnosis-for-chatgpt.md

検証資料は Logs/EventPlacementMigration に保存した。Play Mode用の一時テスト EventPlacementPlaySmoke.cs は同フォルダと検証コピー内だけにあり、通常プロジェクトのAssetsには追加していない。Temp/EventPlacementValidation は元Editorを閉じずに検証するためのコピーである。

## 3. 停止した旧配置経路

MapGenerateから旧EventListSO、EventCategory.evDatas.Clear/登録、CreateEvent、CategoryNamer、assigenList、MainEv、EventRandom、ApplyEvnetへ進む経路を停止した。旧コードはGenerateLegacyMap内に保持し、呼び出し元はない。新Rootが未設定の場合はErrorを出し、旧配置へフォールバックしない。

旧EventListSO引数は呼び出し側との互換性のため残しているが、新規生成では使用しない。TestManegerによる既存設定注入も残っている。

## 4. 背景配置の維持

必須背景を含め、足りないブロック分をランダム背景で補い、シャッフルする方式を維持した。MapGeneratorで全セルにbgCategoryとbackgroundIdを設定してから新イベントを配置する。

ブロック添字は (y / blockSize) * blocksX + (x / blockSize)。旧ループのX/Y逆転を引き継がず、端数領域も設定する。ApplyBackgroundのOdin選択範囲に依存せず同等のセル背景設定を行う。背景生成をEventMapPlacementから分離した。

## 5. EventMapPlacementの変更

既存クラスを更新し、第3の配置クラスは追加していない。Generate自体は既存セルを消さず、既存の新旧イベントを保護する。新規ゲームの状態初期化は呼び出し側のPrepareNewMapが担当する。

必須イベントは、先に候補セルと必要数の割り当てを計画する。背景条件が広い必須イベントが、条件の狭いイベントの唯一の候補を先に使ってしまう場合には、未確定の割り当てを組み替える。配置可能な組み合わせがあれば必要数を確保し、その後Spawnする。全体の空き・適合セルが不足する場合は、イベントコードと配置済み数／必要数をErrorで報告する。既存イベントは上書きしない。

## 6. 必須の判定

有効なカタログ定義のうち、appearance.generateOnMap、解放条件、appearance.conditionsを満たすものを候補とする。その中でappearance.requiredPlacements > 0を必須とし、その数を使用する。名前文字列による判定はない。条件を満たさない・無効な定義を強制配置する仕様にはしていない。

## 7. 拠点

プロジェクト内の新定義を確認したが、拠点用EventDefinitionSOは存在しない。新しい拠点イベントは作らず、GameManager.BaseCell（現在5,5）を使用済み座標へ予約した。SceneのEventSystemRoot.reserveBaseCellはtrue。

将来拠点定義が用意された場合は、requiredPlacementsで通常の必須配置に含め、座標予約設定を見直す。現時点で拠点の名前やカテゴリをコードにハードコードしていない。

## 8. ランダム抽選

出現可能候補のうちrequiredPlacements <= 0、weight > 0かつ有限値の定義を使用する。ブロック内に適合セルがある定義だけを対象にweightで抽選し、選ばれた定義の適合セルから等確率で1セルを選ぶ。必須定義をランダム枠でも再抽選しない。

## 9. ブロック生成

既存のTestManeger.MapGridを流用し、メインSceneでは5。xとyをblockSizeずつ進め、最終ブロックは実際のwidth/heightで切る。15×15は9、20×20は16、25×25は25、17×18は16ブロックとなる。ブロック順はFisher–Yatesでシャッフルする。既存のSystem.Randomを引き続き使用し、大規模なSeed管理は追加していない。

## 10. 使用済み座標

配置処理のローカルHashSet<Vector2Int>で管理する。予約座標、eventInstanceIdがあるセル、EvDataがあるセル、今回Spawnに成功したセルを除外する。MapCellに新しい複数配置禁止制約は設けていない。ランダム配置ができないブロックはWarningを出してスキップする。

## 11. Spawn経路・再生成

定義と座標の決定後は必ずEventManager.Spawnを使用し、Instance生成・Manager登録・セルID設定・Changed通知を通す。配置コードはIDを直接書き込まない。

新規生成時はEventSystemRootから既存Restoreに空のEventSaveDataを渡す。これによりManager、セルID、予約、Runnerキューを同じ経路でリセットし、カードロックと予定表を更新する。その後、旧EvData/eventIdと旧表示を消す。実行中イベントがある場合は再生成を拒否し、途中実行との不整合を避ける。同じRootを別MapDataSOへ誤接続する初期化も拒否する。

既に表示済みなら生成後にMiniMapを再構築し、HakenTabも現在セルに合わせて更新する。新規ゲーム用初期化なので、イベントの予約・解放等の旧ランタイム状態を引き継ぐセーブロード用途には使用しない。

## 12. メインScene設定

- Mainに有効なEventSystemRootを1つだけ追加。
- catalogに既存の通常用イベントカタログを設定。
- uiPrefabにAssets/Prehab/EventSystemUI.prefabを設定。
- ReservationView、ExecutionView、EventScheduleViewは既存InitializeによりPrefab生成後に取得。
- EventManager、MapEventController、EventRunnerも既存Initializeで生成するため、Sceneに同名コンポーネントを増設していない。
- worldStateとexecutionHandlersは空。現在登録されているNarrative定義には外部実行ハンドラーは不要。

## 13. 使用カタログと実データの制限

Assets/イベント/イベントSO/イベントカタログ.assetを使用した。通常用の配置場所・名称を持ち、既存のNewEvent（EVENT_001）を登録済みであることが選定根拠。別カタログはShrineSampleEventCreatorが明示的に生成する神社サンプル用であり、通常用へ自動統合しなかった。

カタログやEventDefinitionSOの内容は変更していない。現在の通常用カタログにはrequiredPlacements > 0の定義がなく、実Sceneではランダム9件のみ生成される。神社サンプルの必須定義は別カタログにある。敵用の新定義は確認できない。必須アルゴリズムはテスト用定義で検証した。

NewEventはmapIconがnullで、文章・効果も未設定。通常データのままではイベントアイコンは出ない。アイコン表示経路の検証にはテスト中のみSpriteを設定し、アセット内容は保存・変更していない。本番コンテンツとして使用するには、該当定義のアイコン・文章・結果と必要な必須定義を設定する必要がある。

## 14. サイズ検証

| サイズ | ランダム件数 | テスト用必須数 | 境界・重複・拠点除外 |
|---|---:|---:|---|
| 15×15 | 9 | 別に3 | 成功 |
| 20×20 | 16 | 別に3 | 成功 |
| 25×25 | 25 | 別に3 | 成功 |
| 17×18 | 16 | 別に3 | 成功 |
| 18×7 | 8 | 別に3 | 成功 |

これらは空き・条件が十分あるテストデータの結果。空き不足のランダム枠はスキップするため常に最大数を保証するものではない。

## 15–16. コンパイルとテスト

Unity 6000.3.10f1で最終46/46成功、失敗0。実行・Editorコードにコンパイルエラーなし。既存のnullable／未使用変数等の警告は残っている。

追加した恒久テストは12ケース。サイズ5ケース、背景条件が競合する必須配置、既存新旧イベントの保護・配置不能Error、出現条件と適合セル、重み・座標の変動、状態再初期化、背景・旧カテゴリ不変・再生成、Scene参照を確認した。

最終結果: Logs/EventPlacementMigration/verified-results.xml

Unityログ: Logs/EventPlacementMigration/verified-unity-tests.log

## 17. Play Mode確認

元Editorを保持したまま、Temp/EventPlacementValidationのメインSceneをバッチ・nographicsでPlay Modeに切り替えて確認した。初期9件、背景、EvDataの消去、セルとManagerの対応、拠点除外、座標変更通知からHakenTabボタンで新予約UIが開くこと、一時Spriteを使った全体図・ミニマップのImage参照、再生成後の旧ID・旧アイコン消去と9件＋プレイヤーアイコンへの更新を確認した。

キーボード操作と画面の目視確認は行っていない。スモークテスト中に検証コピーに含めていないMIDI用micromamba.exeの不足ログが出た。元プロジェクト側には存在するため、コピー環境の音楽依存関係不足であり、イベント配置の失敗ではない。音楽機能の検証は対象外。

## 18. 残事項

- 通常用NewEventのmapIcon・文章・効果は未設定。必要な本番イベント定義の登録も別途必要。
- 拠点はイベントではなく座標予約のみ。
- 元Editorで開いている未保存Sceneを上書き・再ロードしていない。ディスク上のメインSceneへRoot設定を保存済み。元Editor内で古いSceneが開いている場合は、未保存作業を確認してから更新済みSceneを読み直す必要がある。
- 発表用Sceneの新Root接続、旧コード削除、発見・踏破機能、セーブシステム全体、音楽環境は対象外。
- Unityの二重起動失敗と検証コードの一時的な失敗は、別紙unity-startup-diagnosis-for-chatgpt.mdに記録した。
