**イベントシステムの実装・導入メモ**

イベント原本、マップ上の実体、予約、昼夜進行、実行キュー、結果適用、セーブDTO、Odin管理画面を追加しました。新しいUIは `Assets/Prehab/EventSystemUI.prefab` です。既存Sceneへは自動追加していません。以下のInspector設定で新しい配置経路を有効にします。

**調査した既存構造**

- プロジェクトの `ProjectSettings/ProjectVersion.txt` は **6000.3.10f1**。依頼文の6000.2.6f2とは異なります。バージョン設定とPackagesは変更していません。
- リポジトリと親ディレクトリに適用対象の `AGENTS.md` は見つかりませんでした。作業開始時にgit statusを確認しました。
- `GameManager` がパーティ生成、`MapGenerator.MapGenerate`、`TimeManager.Setup`、ミニマップ構築を呼んでいます。
- `EventListSO` は `EventCategory` アセットのリスト。`EventCategory.evDatas` は旧 `EventData` のリストです。旧生成処理はカテゴリから名前・アイコン・好感度・神秘度を持つ `EventData` を生成し、`MapDataSO.ApplyEvnet` が `MapCell.EvData` に入れます。
- 実際のフィールド名は大文字始まりの **`EvData`** でした。参照元はMapDataSO、MapGenerator、MiniMapBuilder、HakenTabです。GameManagerにも旧型の未使用リストがあります。
- `HakenExe` はなく、`HakenTab` が現在地の派遣ボタンを表示しています。新イベントのボタンだけ `MapEventController` へ接続しました。
- 時間は `TimeManager.OnPeriodStarted` を使用。Day→Night→翌Dayの通知が既にあります。
- カードは `CardDrag`、`CardSlot`、`CardCharaManeger`、`Assets/Prehab/カードスロット.prefab` を再利用しています。
- `PartyManager` にGold、Ability、Equip、Hintの付与APIがあり、`PartyRuntime.Members` が実行時キャラクターパラメータを保持しています。
- ショップ、奉納、戦闘を開始・終了する専用システム、ゲーム進行用のセーブ、共通フラグストアは見つかりませんでした。音楽・CSVのファイル保存はゲームセーブとして扱っていません。
- ゲームコードは既定のAssembly-CSharp。既存asmdefはOdinのUnity.Mathematicsモジュールのみでした。新EditorコードとEditModeテストは `Events/Editor` に置き、Assembly-CSharp-Editorへ分離しています。
- Odinは既存MapDataSOやInspector属性で使われています。新データは標準ScriptableObject／Serializableクラスで、Odinの独自シリアライザ型を使っていません。

**採用した構成**

```text
EventDefinitionSO → EventCatalogSO
                          ↓
MapGenerator → EventMapPlacement → EventManager.Spawn
                                      ↓
                      EventInstance ← MapCell.eventInstanceId
                                      ↓
HakenTab → MapEventController → ReservationController / 即時実行
                                      ↓
TimeManager.OnPeriodStarted → Ready → EventRunner（1件ずつ）
                                      ↓
                  Outcome → EventEffectExecutor → 後続イベント → Resolved
```

`EventSystemRoot` が既存GameManagerと各サービスを組み立てます。EventManager、EventRunner、ReservationControllerは通常のC#クラスなので、SceneやUIなしでもテストできます。Viewは表示と操作通知を担当します。

- 原本の `PersistentId` は専用GUID。eventCodeは制作者用コードです。sortOrder、priority、weightは別項目です。
- 管理画面の複製は新GUIDと重複しないeventCodeを発行します。通常のアセット複製によるID重複は全検査で検出し、カタログは曖昧なキーを検索から除外します。
- Scheduledは正のrequiredPhaseCount、Immediateは0。Shop／Offering／EncounterはImmediateです。Immediateに予約カードや必要パラメータを設定すると検査エラーになります。
- 通し時間帯番号は `(CurrentDate - 1) * 2 + (Night ? 1 : 0)`。1日目昼=0、夜=1、2日目昼=2です。
- 予約時にカード数・種類・数量・パラメータ・他イベントの使用状況を確認し、成功後にカードIDを確定します。カード表示は元の場所へ戻し、予約済みのIDをロックします。
- 分割された同一スタックは同じカードIDを共有します。現実装ではそのスタック全体を1件のイベントが占有します。カードの自動消費はせず、消費はOutcomeのEffectで明示します。
- 取消は `Cancelled` という終端状態になり、カードとマップのマスを解放します。完了時もマスを解放します。
- Readyの実行順はpriority降順→reservedPhase昇順→instanceIdのOrdinal順。同時に2つのイベント画面を開きません。
- Narrativeは「判定前→続行で判定→結果表示→続行で結果適用」。SUCCESS／FAILUREを初期値とするOutcomeリストを使い、条件付きOutcomeが合わなければデフォルトへ進みます。
- randomSeed、確定ダイス、合計値、Outcomeコード、適用済みEffect／後続イベントの位置を実体側へ保存します。UIを開き直しても確定判定を振り直しません。
- 結果適用に失敗した場合はRunningのまま原因を表示し、続行で未適用の手順から再試行します。適用済みの報酬を巻き戻す処理ではありません。
- 後続はUnlock、SpawnAvailable、Reserveを指定できます。カードが必須の後続はSpawnAvailableを選び、通常の予約UIで割り当てます。マップが満杯でも実行中の親のマスを後続へ引き継げます。
- 自動で開始するImmediateの循環を検査し、実行時にも祖先IDと最大32段の制限で阻止します。通常の参照ループは警告です。

**Unity Inspectorで必要な設定**

1. **GameManagerとMapGeneratorが付いた同じGameObjectに `EventSystemRoot` を追加**します。GameManagerの既存フィールドは変更不要です。
2. `Assets > Create > Events > Event Catalog` でカタログを作成し、Rootの `catalog` へ設定します。
3. Rootの `uiPrefab` に **`Assets/Prehab/EventSystemUI.prefab`** を設定します。ReservationView、ExecutionView、EventScheduleViewは実行時にPrefabから取得します。独自UIを使う場合は各Viewを直接設定できます。
4. `Tools > Events > Event Database` を開き、同じカタログを選びます。「新規イベント」で原本を作成・登録し、基本／出現／予約／実行／結果を設定します。
5. 必須の文章・画像、eventCode、デフォルトOutcomeを設定して「全イベント検査」を実行します。`appearance.generateOnMap` と `enabled` を有効にしたイベントが配置候補になります。backgroundCategoriesが空なら全背景に配置可能です。requiredPlacementsは最低配置数、weightは各ブロックの追加抽選用です。
6. 既存のHakenTabのTabボタン、GameManagerのPartyManager／MapDataSO／TimeManager、背景リスト、MapGrid設定を利用します。Sceneには既存のUI EventSystemが必要です。
7. ゲーム開始後、イベントマスへ移動して派遣ボタンを押します。Scheduledは予約画面、Immediateは実行キューへ進みます。予約一覧をクリックすると取消できます。時間帯を進めるには既存の `ConsumeTime` または `EndPeriod` を使います。

UI Prefabは既存カードスロットと既存の `NotoSansJP-Bold SDF.asset` を参照します。予約画面ではカードを前面のDragLayerへ移し、閉じた際に元のDragLayerを復元します。左側にログ、右側に画像・ダイス・報酬を置いた実行画面と、スクロールできるスロット／必要パラメータ／予約一覧を用意しました。レイアウトは既存Canvasとは独立しています。

Prefabを別名で再生成する場合は `Tools > Events > Create UI Prefab`。表示確認用画像は `Tools > Events > Capture UI Preview` からLogs内へ生成できます。

**旧EventDataからの移行**

- 旧EventListSO、EventCategory、EventData、MapCell.EvData、その.meta GUIDを削除・改名していません。型の異なるフィールドへFormerlySerializedAsを付けていません。
- EventSystemRootのcatalogを設定しない場合は従来のマップ生成・派遣表示が続きます。新カタログを設定した場合、新しく生成するマップはeventInstanceId経由になります。既存プレイ中データを自動変換する処理ではありません。
- Event Databaseで旧リストと移行先カタログを選び、「旧リストから新アセットを作成」を実行します。旧カテゴリ内にEventDataがあれば各項目から、空ならカテゴリから新原本を作ります。
- 名前・カテゴリ・アイコンをコピーし、旧likeLevel／mysteryLevelと出典はdeveloperMemoに残します。新原本は **enabled=false** で作成します。
- 旧データにない時間、文章、判定基準、報酬は推測で変換しません。原本を編集し、検査後に有効化してください。旧リスト・カテゴリは保存変更しません。
- 原本IDの重複を直すために既存IDを自動再発行する機能はありません。既に使用している原本のIDを維持し、追加した複製側を管理画面から作り直して参照を付け替えてください。

**既存システムへの接続と未接続部分**

| 項目 | 実装・接続先 |
| --- | --- |
| Gold付与・消費 | PartyManager.AddGold / SpendGold |
| Ability／Equip／Hint報酬 | PartyManagerの既存Add API。amount個付与 |
| キャラクターパラメータ | 割当カードのキャラクター。HP／MP／SPは0〜現在最大値へ制限 |
| Goldの出現・Outcome条件 | PartyEventWorldState。resource keyはGold |
| フラグ取得・変更、Gold以外のリソース参照 | **未接続**。IEventWorldState実装をRoot.worldStateへ設定 |
| Shop／Offering／Encounter | **未接続**。IEventExecutionHandler実装をRoot.executionHandlersへ設定 |
| Effectから戦闘開始 | Encounterハンドラへeffect.keyを渡す |
| ゲームセーブのファイルI/O | **未実装**。既存システムがないためDTOと変換のみ |

専用ハンドラは `TryBegin(definition, instance, externalKey, operationId, completion, out error)` を実装します。開始できない場合は副作用なしでfalseを返します。専用画面を閉じた後にcompletionを1回だけ呼び、成功時の文字列にはOutcomeコードを渡します。通常の即時実行はexecution.externalKey、戦闘Effectはeffect.keyを渡します。未接続の場合は原因をログへ出し、イベントを完了扱いにしません。

operationIdは保存・復元後も同じ値です。外部サービスはこのIDを使って二重適用を防いでください。外部処理が進行中の場合、Rootからの別セーブのロードは拒否します。外部画面が終了してからロードしてください。

**セーブDTO**

```csharp
EventSaveData snapshot = eventSystemRoot.CreateSaveData();
string json = UnityEngine.JsonUtility.ToJson(snapshot);
// ファイルへの保存先・書き込みは、将来の既存セーブ機構側で扱う。

EventSaveData loaded = UnityEngine.JsonUtility.FromJson<EventSaveData>(json);
// パーティ／カード／フラグ／マップ／時間を同じスナップショットへ先に復元する。
eventSystemRoot.Restore(loaded);
```

DTOには原本へのScriptableObject参照を保存せず、definitionIdでカタログから再解決します。instanceId、status、座標、予約／実行時間帯、割当カードID、seed、判定結果、Outcomeコード、適用位置、解放済み原本IDなどを保存します。マップの背景、プレイヤー位置、パーティ、時間、外部フラグ自体はこのDTOに含みません。

`CharacterRuntimeData.runtimeId` とカードのIDをパーティ／所持品のセーブにも保存してください。キャラクターカードは `runtimeId/スロット番号` で識別します。キャラクター以外の独自カードは、復元時に `CardDrag.BindEventCard` で同じIDを設定するかIEventCardSourceを接続してください。

RootはTimeManagerとDTOの時間帯番号が一致することを検査します。Manager単体のRestoreはDTO内の時間を復元します。RunningはReadyとしてキューへ戻り、確定判定と適用位置から再開します。存在しない原本、重複した実体ID・マス・カード、不正な予約・結果位置を検出します。問題のある実体は詳細ログを出して無効化し、対応可能な他の実体を復元します。未対応バージョンは現在の状態を維持して拒否します。

原本のOutcomeやEffectの並びをセーブ運用中に変更するためのデータバージョン移行は今回の範囲外です。外部状態とイベントDTOは同じ保存単位で扱う必要があります。

**追加・変更ファイル**

新規C#ファイル（各Unity用.metaも追加）:

| ディレクトリ | ファイル |
| --- | --- |
| Assets/Script/Events/Definitions | EventDefinitionSO.cs、EventDefinitions.cs、EventCatalogSO.cs、EventValidator.cs |
| Assets/Script/Events/Runtime | EventInstance.cs、EventContracts.cs、EventManager.cs、EventCardRegistry.cs、EventRunner.cs、EventEffectExecutor.cs |
| Assets/Script/Events/Integration | EventSystemRoot.cs、EventMapPlacement.cs、MapEventController.cs、PartyEventWorldState.cs |
| Assets/Script/Events/UI | ReservationController.cs、ReservationView.cs、ExecutionView.cs、EventScheduleView.cs |
| Assets/Script/Events/Editor | EventDatabaseWindow.cs、EventUiPrefabBuilder.cs、EventUiPreview.cs |
| Assets/Script/Events/Editor/Tests | EventSystemTests.cs |

既存ファイルの変更:

| ファイル | 変更内容 |
| --- | --- |
| Assets/Script/マップ/MapCell.cs | eventInstanceIdを追加。EvDataを維持 |
| Assets/Script/マップ/Map成形/MapGenerator.cs | 新カタログへの切替、入力検査。背景候補の取得を再利用 |
| Assets/Script/マップ/Map成形/MiniMapBuilder.cs | 新イベントのアイコンと色キーを解決 |
| Assets/Script/マップ/MapDataSO.cs | Editorの描画コードをUNITY_EDITOR内へ分離 |
| Assets/Script/UI/イベント/派遣/HakenTab.cs | 新イベント選択の委譲、状態変更時の表示更新、購読解除 |
| Assets/Script/UI/カード/CardDrag.cs | 実行時カードID・ロック、スロット参照の解除、分割と返却の修正 |
| Assets/Script/UI/カード/CardSlot.cs | 公開CanAcceptとイベント用設定、数量・ロック検査、イベントスロットへの配置 |
| Assets/Script/UI/カード/CardCharaManeger.cs | キャラクターIDに基づくカードIDを設定 |
| Assets/Script/プレイヤー/CharacterRuntimeData.cs | 保存可能なruntimeIdを追加 |
| Assets/Script/ゲーム進行/入力/MoveReader.cs | 既存のユーザー変更を保持し、イベント画面中の移動抑止を1行追加 |

そのほか、`Assets/Prehab/EventSystemUI.prefab`、`Tools/Events/RunEditModeTests.ps1`、このドキュメントを追加しました。既存のGameManager、TimeManager、PartyRuntime、Scene、Canvas.prefabの作業中変更を上書きしていません。既存C#のCP932エンコーディングも保持しています。

**検証と残る課題**

- プロジェクトの参照DLLを使ったランタイム／Editor C#コンパイルを実施。その後、実際のUnity 6000.3.10f1でもコンパイル・EditModeテストを実行しました。
- **最終結果: EditMode 25件中25件成功、失敗0、Unity終了コード0。** 予約・実行UIも隔離したプレビューSceneで描画し、左右配置・日本語・スロット・必要値・報酬表示を確認しました。
- テストはカタログ検索・重複、複数実体とマス接続、予約と期日、即時実行、実行順、原本の不変性、DTO復元、欠落原本、取消・重複カード、既存昼夜通知、外部ハンドラの待機、結果適用の再開、循環防止、長方形マップ、満杯マップの後続、カード受入・返却を対象としています。
- 再実行はプロジェクトルートで `./Tools/Events/RunEditModeTests.ps1`。Unityが必要とするキャッシュ・ライセンスへのアクセスが可能な通常権限で実行してください。結果は `Logs/EventSystemVerification/editmode-results.xml` と `unity-tests.log` です。
- 6000.2.6f2での実行、既存Sceneでマウス操作を行うPlayMode通し確認、プレイヤービルドは未実施です。既存のGeneterScoreなどにはEditor APIへの依存が残っており、プレイヤービルド向けの整理は別途必要です。
- 次に行う項目は、実イベント原本の作成・有効化、SceneのRoot／カタログ設定、専用ショップ・奉納・戦闘・フラグへの接続、パーティと時間を含む統合セーブ、既存Scene上での操作確認です。
