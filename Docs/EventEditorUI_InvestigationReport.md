# イベント編集UI 調査報告書

調査日：2026-10-11（日本時間）  
対象：`G:/Unity/Product/Music_Map_Exp`  
目的：この文書だけを資料として、初心者向けの正確な操作手順書を作成できるようにする。

## 0. 調査方法・確認範囲・読み方

ソースコード、Editor拡張、ScriptableObjectの定義、Scene／Prefab／既存イベントの設定、保存済みテスト結果を読み取った。**今回Unityは起動せず、画面操作・コンパイル・テストの再実行もしていない。以下の実装済み機能も、今回の調査ではすべて「実機動作未確認」である。** コード上の存在、過去の自動テスト記録、今回の実機検証は区別する。

既存のゲームコード・UI・ScriptableObject・Scene・Prefabには変更を加えない調査である。報告書内の設定例は実施済みの変更ではなく、後で人が操作するための例。commit／pushは行わない。

### 表示名の確度

- **明示名**：`LabelText`、`EditorGUILayout`、`Button`、`FoldoutGroup`、`TabGroup`、`MenuItem`の文字列をそのまま記載する。
- **自動ラベル（†）**：C#名をOdin／Unityが整形して表示する見込みの名称。例：`deadlinePhaseCount` → `Deadline Phase Count`。実画面の大文字・空白・日本語化は未確認。併記した内部名を識別の基準にする。
- **List操作（L）**：リストを展開し、Odin標準の追加操作で要素を作り、要素の削除操作で除く。通常は「+」、削除ボタンまたは項目メニュー、並べ替えハンドルが使われるが、**本プロジェクトの実画面における記号・配置・メニュー文言は未確認**。独自の「条件追加」「条件削除」ボタン実装はない。
- **Object参照（O）**：該当型のProjectアセットを参照欄へドラッグ、またはUnity標準のオブジェクト選択操作で選ぶ。アセット名やGUIDを自由入力する欄とは異なる。

以下の項目表は、要求された情報を「UI名」「内部変数と型」「役割・ゲームへの影響」「入力・候補」「初期値」「例」「注意」「根拠」に整理する。初期値の「空」はstringのnull／空表示、「未設定」はObjectのnull、「[]」は空List。既存アセットの値と新規初期値は別である。

### 最初に知るべき重要事項

1. **従来方式と拡張方式の2系統がある。** 新規イベントの`useExtendedLifecycle`はfalse。今回の7グループUIを使うには有効化する。
2. **Conditionsは発生可能性の検査であり、時刻トリガーではない。** `World == Night`だけでは、昼に始まったマップへ夜になった瞬間に追加生成されない。
3. **Deadline／Duration／FollowUp Delayは別物。** 0の意味も、期限切れ／Start直後の解決待ち／親解決後の即時出現で異なる。
4. **拡張側と従来側のOutcomeは別データ。** 切替を入れただけで旧報酬等が拡張側へ移行するわけではない。
5. **Validatorは保存・再生を禁止するゲートではない。** エラーがあっても保存でき、実行時の拒否・停止・不正表示につながる場合がある。

## 1. 正式名称と根拠ファイル索引

以後の`[Sxx]`はこの索引のファイル、クラス、メソッドを指す。パスは上記プロジェクトルートからの相対パスで、外部資料なしで対象を特定できる。

| ID | ファイル | 正式クラス・主な調査箇所 |
|---|---|---|
| S01 | `Assets/Script/Events/Definitions/EventDefinitionSO.cs` | `EventDefinitionSO`、全フィールド、`EffectiveOutcomes`、`OnValidate` |
| S02 | `Assets/Script/Events/Definitions/EventDefinitions.cs` | `EventAppearanceDefinition`、`EventReservationDefinition`、`CardSlotDefinition`、`EventRequirement`、`EventExecutionDefinition`、`EventJudgeDefinition`、`EventPresentation`、`EventCondition`、`EventOutcomeDefinition`、`EventFollowUp`、`EventEffect` |
| S03 | `Assets/Script/Events/Definitions/EventExpansion.cs` | `EventQuery`、`CharacterInstances`、`Operators`、`OccurrenceCondition`、`PointCondition`、`EventExpansion`、`OutcomeEffect`と5派生型 |
| S04 | `Assets/Script/Events/Definitions/EventCatalogSO.cs` | `EventCatalogSO.Initialize`、`TryGetById`、`TryGetByCode`、`GetEnabled` |
| S05 | `Assets/Script/Events/Editor/EventDatabaseWindow.cs` | `EventDatabaseWindow`、`Create`、`CreateAsset`、`Duplicate`、`RegisterSelected`、`DrawList`、`DrawSelectedInspector`、`ValidateAll`、`DrawIssues`、`DrawReferences`、`ImportLegacy` |
| S06 | `Assets/Script/Events/Editor/EventCategoryEditorTools.cs` | `EventDefinitionInlineEditor.OnInspectorGUI`、`EventDefinitionSOEditor`、カテゴリSelector／Picker／Create／Manager、CatalogResolver、EditorUtility、EditorLifecycle |
| S07 | `Assets/Script/Events/Editor/EventExpansionEditorModel.cs` | `EventExpansionEditorModel`全プロパティ、`OutcomeEditor`、`Validation` |
| S08 | `Assets/Script/Events/Definitions/EventCategoryCatalogSO.cs` | `EventCategoryCatalogSO`、`EventCategoryDefinition`、`NormalizeDisplayName`、`EditorTryAdd`、`EditorTryUpdate` |
| S09 | `Assets/Script/Events/Editor/EventCategoryMigrationWindow.cs` | `EventCategoryMigrationWindow.DrawMigration`／`DrawRow`、`EventCategoryMigration.Assess` |
| S10 | `Assets/Script/Events/Definitions/EventValidator.cs` | `EventValidator.Validate`、ローカル`CheckCategory`／`CheckPresentation`／`CheckReference`、`Edges`／`HasCycle`、`EventIssueSeverity` |
| S11 | `Assets/Script/Events/Definitions/EventExpansionValidator.cs` | `EventExpansionValidator.Validate`、ローカル`Query` |
| S12 | `Assets/Script/Events/Runtime/EventManager.cs` | `EventManager.Spawn`、`Reserve`、`ValidateCards`、`AdvancePhase`、`CancelReservation`、`StartFollowUp`、`FindFreeCell`、`CreateSaveData`、`Restore` |
| S13 | `Assets/Script/Events/Runtime/EventManagerExpansion.cs` | `CanAppear`、`Evaluate`、`TryBuildAssignments`、`StartExtended`、`Expire`、`CalculateHintLevel`、`RecordHistory`、`ScheduleFollowUp`、`ProcessFollowUps` |
| S14 | `Assets/Script/Events/Runtime/EventRunner.cs` | `EventRunner.Pump`、`Continue`、`ApplyNext`、`Tick`、`Report` |
| S15 | `Assets/Script/Events/Runtime/EventProgress.cs` | `EventHistoryRecord`、`EventProgress`、`FlagState`、`HintOwnership`、`HintDrawHistory`、`FacilityState`、`ScheduledFollowUp`、`EventEffectContext.GrantHint` |
| S16 | `Assets/Script/Events/Runtime/EventInstance.cs` | `EventInstance`、`Copy`、`EventSaveData` |
| S17 | `Assets/Script/Events/Runtime/EventEffectExecutor.cs` | `TryApplyOutcome`、`TryApply` |
| S18 | `Assets/Script/Events/Runtime/EventCardRegistry.cs` | `EventCardRegistry.AssignedCharacters`、`TryGetParameter`、`Characters`、`TryCharacter`、`FindCard`、`RefreshLocks` |
| S19 | `Assets/Script/Events/Runtime/EventContracts.cs` | `IEventCardSource`、`IEventWorldState`、`IEventExecutionHandler`、`EventContext.Matches` |
| S20 | `Assets/Script/Events/Integration/EventMapPlacement.cs` | `Generate`、`MatchesBackground` |
| S21 | `Assets/Script/Events/Integration/EventSystemRoot.cs` | `Initialize`、`CurrentPhase`、`OnPeriodStarted`、`RefreshSchedule`、`PrepareNewMap`、`SyncOwnedHints`、`Restore` |
| S22 | `Assets/Script/Events/Integration/MapEventController.cs` | `MapEventController.Select` |
| S23 | `Assets/Script/Events/UI/ReservationView.cs`、`ReservationController.cs` | `Show`、`Open`、`Confirm`、`Close` |
| S24 | `Assets/Script/Events/UI/ExecutionView.cs`、`EventScheduleView.cs` | `ExecutionView.Show`／`ShowError`、`EventScheduleView.Show` |
| S25 | `Assets/Script/UI/カード/CardDrag.cs`、`CardSlot.cs`、`Assets/Script/Events/Integration/EventCardIdentity.cs` | `CardType`、`BindEventCard`、`OnBeginDrag`、`OnCardDropped`、`CanAccept`、`EventAutomatic`、`RestoreIdentity` |
| S26 | `Assets/Script/ゲーム進行/TimeManager.cs`、`入力/MoveReader.cs` | `Setup`、`ConsumeTime`、`EndPeriod`、`OnPeriodStarted`、`MoveReader.OnMove`／`WaitTime` |
| S27 | `Assets/Script/GameManager.cs`、`Assets/Script/マップ/Map成形/MapGenerator.cs` | `GameManager.Start`、`ReturnPlayerToBase`、`MapGenerator.MapGenerate` |
| S28 | `Assets/Script/UI/イベント/派遣/HakenTab.cs` | `HakenTabView`、`HakenTabOut`、新Instanceセルから`MapController.Select`への接続 |
| S29 | `Assets/Script/Events/Definitions/EventIdentitySO.cs`、`FlagDefinitionSO.cs`、`FacilityDefinitionSO.cs`、`HintPoolSO.cs`、`Assets/Script/Scritable/HintData.cs` | 共通安定ID、表示名・説明、Flag Scope、Hint Pool |
| S30 | `Assets/Script/Events/Editor/EventExpansionSampleCreator.cs`、`ShrineSampleEventCreator.cs`、`EventUiPrefabBuilder.cs`、`EventUiPreview.cs` | サンプル・ゲーム内UI生成とCaptureメニュー |
| S31 | `Assets/Script/発表用/HintSelectionManager.cs`、`Emo5Toggle.cs`、`AnswerSet.cs` | `HintSelectionState`、`EnforceConstraints`、楽曲生成用選択 |
| S32 | `Assets/Script/マップ/Map背景/BackgroundCategory.cs`、`BackgroundCategoryListSO.cs`、`Assets/Script/マップ/Map成形/MiniMapBuilder.cs`、`MiniMapVisualConfig.cs` | `BackgroundCategory`、`BGCategoryListSO`、`CellInt`、`ResolveCellColor` |
| S33 | `Assets/Script/プレイヤー/CharacterRuntimeData.cs`、`パーティー/PartyManeger.cs`、`パーティー/PartyRuntime.cs`、`Assets/Script/Scritable/キャラ/CharaDeta.cs` | 正式名`CharacterRuntimeData`／`PartyManager`／`PartyRuntime`／`CharaData`。ファイル名の綴りとクラス名が異なる |
| S34 | `Assets/Script/Events/Editor/Tests/EventExpansionTests.cs`、`EventSystemTests.cs`、`EventCategoryTests.cs`、`EventPlacementTests.cs` | 保存されている自動テストの実装。今回未実行 |

「EventHistory」という独立SOやEditor画面はない。正式データ名は`EventHistoryRecord`で、`EventProgress.history`に格納される。「Condition」は従来の`EventCondition`と、拡張の`OccurrenceCondition.query: EventQuery`を区別する。

## 2. UI全体構造と共通操作

### 2.1 開くメニュー・選択アセット

| 正確なコード上のメニューパス | 用途・前提 | 根拠 |
|---|---|---|
| `Tools > Events > Event Database` | イベント一覧・作成・登録・編集・検査。開いた後にCatalogを選ぶ | S05.Open |
| `Tools > Events > Event Categories` | 共有カテゴリ管理。選択中のEventCategoryCatalogSO、またはEventCatalogSOのcategoryCatalogを使用 | S06.EventCategoryManagerWindow.OpenFromSelection |
| `Tools > Events > Create Category Catalog and Connect` | 選択中のEventCatalogSOへ空カテゴリカタログを作成・接続。EventCatalogSO選択時のみ有効。既に接続済みなら作り直さない | S06.EventCategoryCatalogAssetMenu |
| `Tools > Events > Category Migration` | 旧カテゴリ文字列の移行支援。通常の新イベント作成には不要 | S09.OpenFromSelection |
| `Tools > Events > Create Extension Sample` | 専用フォルダに拡張サンプルを作成。既存PhaseSampleがあれば何もしない | S30.EventExpansionSampleCreator.Create |
| `Tools > Events > Create Shrine Sample Event` | 既存の簡易神社サンプル作成。拡張サンプルとは別 | S30.ShrineSampleEventCreator |
| `Tools > Events > Create UI Prefab` | プレイヤー向け予約／実行／予約一覧UIのPrefab作成。イベント編集画面作成ではない | S30.EventUiPrefabBuilder.Create |
| `Tools > Events > Capture UI Preview` | ゲーム内UIのプレビュー画像を生成するコード。Editorの7グループ画面の撮影機能ではない | S30.EventUiPreview.Capture |
| `Assets > Create > Events > Event Definition` | EventDefinitionSOを直接作成。Database経由とは初期Code・カテゴリ移行状態・登録処理が異なる | S01.CreateAssetMenu |
| `Assets > Create > Events > Event Catalog` | 定義リストを持つカタログ | S04 |
| `Assets > Create > Events > Event Category Catalog` | 共有カテゴリ辞書。作成しただけではEventCatalogへ未接続 | S08 |
| `Assets > Create > Events > Flag`／`Facility`／`Hint`／`Case Hint Pool` | 新方式用の参照アセット | S29 |

`Assets > Create`はCreateAssetMenuをUnityが表示する標準経路。Project右クリックのCreateにも対応する通常の操作として案内できるが、その画面自体は今回未確認。

Projectで**EventDefinitionSOの.asset**を選ぶと、`EventDefinitionSOEditor → EventDefinitionInlineEditor`が表示される。Database内の「選択イベント」も同じEditorを埋め込む。EventCatalogSOを選ぶとカタログ参照／定義リストのInspector、EventCategoryCatalogSOを選ぶとカテゴリ数・問題表示・「カテゴリ管理を開く」が表示される。EventInstanceを編集するアセット選択経路はない。

### 2.2 階層・グループ数・表示順序・色

```text
Event Database
├─ イベント一覧（独自描画、PropertyOrder(-1)）
│  ├─ すべて / 未設定 / 特定カテゴリ…
│  └─ eventCode   displayName   [カテゴリ] の行ボタン
├─ カタログ：Catalog†／新規カタログ／カテゴリ作成・接続／カテゴリ管理
├─ 検索：Search†／Tag†／一覧を更新
├─ 編集：新規イベント／新しいGUIDで複製／選択イベントをカタログへ登録
├─ 検査：全イベント検査／件数／各指摘の「開く」
├─ 参照：参照元ボタン（従来outcomesのみ）
├─ 選択イベント
│  ├─ 共有イベントカテゴリ
│  └─ EventDefinition Inspector（下記）
└─ 旧データ移行：Legacy List†／旧カテゴリ対応／移行・取り込みボタン

EventDefinition Inspector
├─ 共有イベントカテゴリ（常時、7グループとは別）
│  ├─ 選択中／検索して選択／未設定に戻す／新規追加／カテゴリ管理
│  ├─ 内部IDを確認（折りたたみ、Category IDは読み取り専用）
│  └─ 移行待ちの場合のみ：カテゴリ移行支援を開く
├─ useExtendedLifecycle = false
│  └─ Odin通常描画：基本／Extension／出現／予約／実行／結果 の6タブ
│     （実際のタブ順・全プロパティの自動表示範囲は未確認）
└─ useExtendedLifecycle = true かつ extension != null
   ├─ 拡張ライフサイクル（チェックボックス）
   ├─ 1 基本情報
   ├─ 2 発生条件
   ├─ 3 Slot設定
   ├─ 4 時間設定
   ├─ 5 HintLevel設定
   ├─ 6 Outcome設定 → SUCCESS / FAILURE / EXPIRED タブ
   └─ 7 Validator
```

**拡張の主要グループは7個**。いずれもFoldoutGroup。上記1～7はS07の宣言順で、明示的なPropertyOrderはないため実画面の順序は未確認。DatabaseのBoxGroupは「カタログ・検索・旧データ移行・編集・検査・参照・選択イベント」の**7個**で、これにグループ属性外の「イベント一覧」が加わる。図のDatabase順は機能を理解するための並べ方で、実画面の縦順を保証するものではない。

| 拡張グループ | コードで指定されるRGB | 色指定の対象 |
|---|---|---|
| 基本情報 | (.85,.85,.85) 灰 | DisplayName |
| 発生条件 | (.65,.8,1) 青 | Conditions |
| Slot設定 | (.7,1,.7) 緑 | Slots。Slot内のlabelにも指定 |
| 時間設定 | (1,.95,.6) 黄 | DeadlinePhaseCount |
| HintLevel設定 | (.85,.65,1) 紫 | PointConditions |
| Outcome設定 | (1,.75,.75) 赤系 | Success |
| Validator | (1,.8,.5) 橙 | Validation描画メソッド |

`GUIColor`は各メンバーに付いた指定であり、「グループ全体の背景が必ずこの色」とは断定できない。テーマ・子要素への適用の見え方は実機動作未確認。S03の汎用EventExpansionにも英語グループと色指定があるが、拡張有効時の通常InspectorはS07の日本語グループを描画する。

### 2.3 Databaseの全操作項目

| UI名 | 内部変数・型 | 役割・影響 | 入力・候補 | 初期値 | 例 | 注意 | 根拠 |
|---|---|---|---|---|---|---|---|
| Catalog† | catalog: EventCatalogSO | 登録先、カテゴリ解決、検査の文脈 | O | 未設定 | 検証用Catalog | 選択しただけでSceneのCatalogは変わらない | S05.OnCatalogChanged |
| Search† | search: string | Codeまたは表示名の部分一致検索 | 文字入力 | 空 | 飲食 | 大文字小文字無視 | S05.DrawList／Contains |
| Tag† | tag: string | tags中の1要素との完全一致検索 | 文字入力 | 空 | restaurant | 大文字小文字無視。タグ追加欄ではない | S05.DrawList |
| すべて／未設定／特定カテゴリ… | categoryFilterMode／categoryFilterId | カテゴリで絞る | ボタン・選択窓 | All | 飲食店 | 全Projectの定義を検索。Catalog内限定ではない | S05.DrawCategoryFilter |
| 一覧を更新 | Refresh() | AssetDatabaseから定義一覧を再取得 | ボタン | ― | 新アセット追加後 | sortOrder→eventCode順 | S05.Refresh |
| 新規イベント | Create() | 保存場所指定→GUID付き定義生成 | ボタン＋保存ダイアログ | 提案名NewEvent | RestaurantNight.asset | 選択Catalogがあればその場で登録。カテゴリ自動選択なし | S05.Create／CreateAsset |
| 新しいGUIDで複製 | Duplicate() | 設定を複製し、GUIDとCodeを別にする | 選択後ボタン | ― | 既存イベントの派生 | Codeは元Code_COPY等。普通のProject複製はGUIDフィールドも複写する | S05.Duplicate |
| 選択イベントをカタログへ登録 | RegisterSelected() | definitionsへ追加し保存 | ボタン | ― | 直接作成した定義 | Catalog／選択イベントがない、登録済みなら何もしない | S05.RegisterSelected |
| 新規カタログ | CreateCatalog() | 空EventCatalogSOを作成・選択 | ボタン＋保存ダイアログ | 空リスト | RestaurantTestCatalog | categoryCatalogは別途接続 | S05.CreateCatalog |
| カテゴリカタログを作成・接続 | CreateCategoryCatalog() | 空辞書を作成・Catalogに接続 | ボタン | ― | 初回準備 | Catalog未選択ならダイアログ | S05／S06 |
| カテゴリ管理を開く | OpenCategoryManager() | 管理窓を開く | ボタン | ― | 飲食店カテゴリ追加 | カテゴリ辞書未設定なら案内 | S05 |
| 全イベント検査 | ValidateAll() | 全定義＋選択Catalogを検査 | ボタン | 指摘[] | 保存前 | 検査結果はこのボタンで更新。G章参照 | S05 |
| 開く | issue.asset | 指摘対象を選択・Ping | 指摘横のボタン | ― | 欠落参照を修正 | 定義なら選択イベントも切替 | S05.DrawIssues |
| 参照元: Code | DrawReferences() | 選択定義を参照する元を開く | 行ボタン | ― | 後続の親を確認 | **拡張のextension.*.followUpsを検索しない** | S05.DrawReferences |
| Legacy List† | legacyList: EventListSO | 旧イベント取り込み元 | O | 未設定 | 旧EventListSO | 新方式のCatalogとは別型 | S05 |
| 新イベントの旧カテゴリ文字列を移行 | OpenCategoryMigration() | Category Migration窓 | ボタン | ― | 既存移行待ち資産 | 表示だけでは移行しない | S05／S09 |
| 旧リストから新アセットを作成 | ImportLegacy() | 旧データから新SOを作成 | ボタン・対応表・フォルダ指定 | ― | 旧リスト取り込み | 時間・文章・判定・報酬は補完せずenabled=falseで作成。拡張方式への全変換ではない | S05.ImportLegacy |

### 2.4 新規・編集・削除・登録・保存

**新規の推奨経路**：Databaseを開く→Catalog指定→「新規イベント」→Assets内へ保存→共有カテゴリを選ぶ→ExtensionタブでUse Extended Lifecycle†をON。Database経由はCodeを`EVENT_001`等の一意名、表示名をファイル名、カテゴリ移行状態をMigratedにする。直接Createメニュー経由ではeventCode／displayNameは空、カテゴリは未設定、移行状態はPendingの初期値なので、必要項目の補完と登録が必要。

**既存編集**：Databaseの行を押すか、Projectで定義.assetを選択。上部の共有カテゴリを含めて編集する。Catalogに含まれないイベントもDatabase一覧に出る。「表示される＝ゲームに登録済み」ではない。

**削除**：Databaseにイベント削除ボタン／安全な参照追跡付き削除機能はない。実装上は、対象EventCatalogSOの`Definitions†`から参照を外す方法と、`Enabled†=false`で発生候補から除外する方法がある。アセットファイル自体の削除はUnity標準Project操作に委ねられる（Delete等の実画面操作は未確認）。削除前に全Catalog、Conditions／PointConditionsのイベント参照、全OutcomeのFollowUps、旧Effectの参照、既存セーブを確認する。Databaseの参照元欄だけでは新方式の参照を網羅できない。カテゴリも専用削除ボタンはなく、使用停止を使う。

**保存**：作成・登録・カテゴリ更新はコードが`SaveAssetIfDirty`またはGUI後の遅延保存を呼ぶ。拡張Inspectorの一般編集は`Undo.RecordObject`と`EditorUtility.SetDirty`までで、毎操作のディスク保存呼出しはない。編集後はUnity標準のProject／アセット保存を行い、再選択・再読み込みで保持を確認すること（標準のSave Project等の具体的表示位置とCtrl+Sの対象範囲は今回未確認）。**イベント編集画面全体に独自の「保存」ボタンはない**。「変更を保存」はカテゴリ管理だけのボタン。Play中のランタイム状態の保存とは無関係。

## A. 基本情報

### A.1 定義全体の項目

S07のプロパティは元のS01フィールドを直接編集する。表の「旧画面のみ」は拡張ONの専用画面に露出していない項目を意味し、データの消滅を意味しない。

| UI名 | 内部変数・型 | 役割・ゲームへの影響 | 入力・候補 | 初期値 | 設定例 | 注意点 | 根拠 |
|---|---|---|---|---|---|---|---|
| Use Extended Lifecycle†／拡張ライフサイクル | useExtendedLifecycle: bool | 使用する設定系統を切替 | checkbox | false | true | ONで7グループ。旧設定を変換しない | S01／S06.OnInspectorGUI |
| Definition Id†／Persistent Id† | persistentId: string | セーブ・参照用の定義GUID | 読取専用 | 自動32桁GUID | 自動値を維持 | 人がCodeを設定する操作とは別。複製はDatabaseを使う | S01／S05.CreateAsset |
| Display Name† | displayName: string | イベント名、ゲーム内タイトル | 文字 | 直接作成は空／Databaseはファイル名 | 夜の食堂 | 空白だけは全体検査Error | S01／S07／S23.Show |
| Event Code† | eventCode: string | 人が扱う識別子、一覧・検索・Code検索 | 文字 | 直接作成は空／DatabaseはEVENT_001等 | RESTAURANT_NIGHT | 全Project検査は大小文字無視の重複をErrorにする | S01／S04／S05 |
| Enabled† | enabled: bool | 新規配置・Start許可 | checkbox | true | true | OFFは進行中の全Instanceの取消を意味しない | S12／S13 |
| Priority† | priority: int | Readyの処理順、高い方を先に | 整数 | 0 | 10 | 出現確率ではない。次にreservedPhase、Instance IDで整列 | S14.Pump |
| Tags†（旧画面のみ） | tags: List<string> | Database／Catalogのタグ絞り込み | L＋文字 | [] | restaurant, story | 共有タグ辞書なし。新しい文字列をリストへ追加する。拡張専用画面に項目なし | S01／S04／S05／S07 |
| Developer Memo†（旧画面のみ） | developerMemo: string | 制作者用メモ | 複数行文字 | 空 | テスト用、難易度仮 | ゲーム内の本文ではない | S01／S07 |
| Sort Order†（旧画面のみ） | sortOrder: int | 一覧／Catalog列挙の並び順 | 整数 | 0 | 20 | Priorityとは別 | S01／S04／S05 |
| 共有イベントカテゴリ／選択中 | categoryId: string | 共有カテゴリ定義への参照 | 専用検索・選択 | 未設定 | 飲食店 | 生ID入力は禁止された表示。カテゴリだけでは飲食店の座標や背景にならない | S06／S08 |
| 旧カテゴリ移行待ち表示 | category: string／categoryMigrationState: enum | 旧値の監査と移行状態 | 移行窓 | 空／直接作成Pending | 既存値を確認 | 通常フィールドは非表示。Database新規はMigrated | S01／S06／S09 |

Tags等を操作する必要がある場合、定義編集時に一時的に拡張をOFFにして従来「基本」タブを開く経路はコード上存在する。拡張データはその操作で削除されないが、**OFFのまま保存・再生すると動作方式が変わる**。戻し忘れに注意し、初心者には必須作業として案内しない。

### A.2 Map Placement†（旧画面の「出現」に相当）

| UI名 | 内部変数・型 | 役割・ゲームへの影響 | 入力・候補 | 初期値 | 設定例 | 注意点 | 根拠 |
|---|---|---|---|---|---|---|---|
| Map Icon† | appearance.mapIcon: Sprite | マップ上のアイコン | O | 未設定 | 食堂用Sprite | 未設定はWarning。Instanceが存在してもアイコンを作らない場合あり | S02／S20／S21.OnChanged |
| Map Color Key† | appearance.mapColorKey: string | ミニマップ色判定へ渡すキー | 文字 | 空 | 初期検証は空 | Colorピッカーではない。MiniMapVisualConfigの実装に合うキーが必要 | S02／S32.CellInt |
| Generate On Map† | appearance.generateOnMap: bool | 初期マップ生成の候補に含める | checkbox | true | 後続専用ならfalse | FollowUpのSpawnを禁止する値ではない | S20.Generate／S13.ProcessFollowUps |
| Requires Unlock† | appearance.requiresUnlock: bool | 初期生成候補にアンロック状態を要求 | checkbox | false | 通常false | 単独で解除UIはない。拡張5EffectにUnlockEffectはない | S20／S12.Unlock |
| Required Placements† | appearance.requiredPlacements: int | 初期生成で要求する配置数 | 0以上整数 | 0 | 1 | 発生条件が成立した候補についてのみ。空き／背景が足りなければ配置不足ログ | S20.Generate |
| Weight† | appearance.weight: float | 必須数0の定義をランダム選択する相対重み | 0以上数値 | 1 | 2 | パーセントではない。Required Placements>0の定義はランダム候補から除外 | S20.Generate |
| Background Categories† | appearance.backgroundCategories: List<string> | 初期配置可能な背景カテゴリを絞る | L＋文字 | [] | 実在bgCategoryNameと同じ文字 | 空は制限なし。Object選択でなく完全一致文字列。FollowUpの空きセル検索では参照しない | S20.MatchesBackground／S12.FindFreeCell |
| Conditions†（Map Placement内部） | appearance.conditions: List<EventCondition> | **従来型の追加発生条件** | L＋Kind等 | [] | 通常は空のまま | B章の発生条件/Conditionsと別。初期生成では両方必要、直接Spawn／拡張FollowUpはこの旧Listを評価しない | S19／S20／S13 |

ランダム配置はマップをblockSize区画に分け、各区画で条件を満たす空きセルと定義から最大1件選ぶ。カテゴリ「飲食店」と背景カテゴリは独立している。背景を増やすなら`Assets > Create > Map > BackgroundCategory`で`BackgroundCategory.bgCategoryName`等を設定し、`Assets > Create > Map > Background List`で作る`BGCategoryListSO.bGCategoryList`をGameManagerの背景リストへ接続する別作業になる。今回、その追加操作は未実施。[S20、S27、S32]

### A.3 文章・画像・判定

`EventPresentation`の全項目は共通で、`Text† → text: string`（複数行、初期空、例「食堂で聞き込みをする」）と`Image† → image: Sprite`（O、未設定、例食堂Sprite）。以下の使用場所ごとに別の値を持つ。

| UIの場所 | 内部変数・型 | 役割・影響 | 入力・候補／初期値 | 例 | 注意 | 根拠 |
|---|---|---|---|---|---|---|
| Start Presentation† | reservation.reservationPresentation: EventPresentation | カード配置画面の文章・画像 | 上記Text／Image、空／null | 「人物を1人選ぶ」 | マップ背景画像と別。Required表示に文章を入れても検査条件は増えない | S02／S23.Show |
| Execution† > Before Judge† | execution.beforeJudge: EventPresentation | Ready後、判定前に表示 | 同上 | 「店主に話を聞いた」 | Duration経過後に表示される | S14.Pump／S24.Show |
| Outcome > 各タブ > Presentation† | extension.success／failure／expired.presentation | 結果文章・画像 | 同上 | 「情報を得た」 | EXPIREDは現行Runnerが表示を飛ばし効果へ進む。設定した期限切れ文章が表示されるとは言えない | S14.Pump／Continue |
| Execution Type† | execution.executionType: enum | 実行ハンドラ選択 | Narrative / Shop / Offering / Encounter、Narrative | Narrative | 拡張はNarrativeのみValidatorが許容。実Sceneの外部ハンドラは空 | S02／S11／S21 |
| External Key† | execution.externalKey: string | 外部ハンドラに渡す識別子 | 文字、空 | Narrativeでは空 | 初期拡張シナリオには不要 | S14.Pump |
| Use Dice† | execution.judge.useDice: bool | ダイス加算の有無 | checkbox、true | false | falseでもDice Count／Sidesの妥当性検査は実行される | S14.Continue |
| Parameter† | execution.judge.parameter: EventParameter | 全割当人物から合計する能力 | 9候補、Appeal | Appeal | 施設・Equip自体の数値をこの合計へ加えない | S18.TryGetParameter |
| Dice Count† | execution.judge.diceCount: int | 振る個数 | 整数、1 | 1 | 1～100 | S11／S14 |
| Dice Sides† | execution.judge.diceSides: int | ダイスの面数 | 整数、6 | 6 | 2～10000 | S11／S14 |
| Target† | execution.judge.target: int | 成功の下限 | 整数、4 | 5 | HintLevelの閾値とは別 | S14.Continue |
| Modifier† | execution.judge.modifier: int | 合計への固定加算 | 整数、0 | 0 | 負数も設定可能 | S14.Continue |

成功式：`現在の割当人物のParameter合計 + Modifier + ダイス合計 >= Target`。人物の能力をStart時にSnapshot保存する方式ではない。ダイスはInstanceのseedから固定アルゴリズムで計算される。`Use Dice=false, Target=0`は一般に成功を確認しやすい例だが、負の能力／Modifierがあるなら成功保証にはならない。

### A.4 カテゴリの追加・変更とCatalog

1. DatabaseのCatalogを選び、categoryCatalog未設定なら「カテゴリカタログを作成・接続」。
2. 選択イベント上部「共有イベントカテゴリ」の「新規追加」を押す。
3. 「名前」「説明」「表示順」を入力→「作成」。この経路は作成したカテゴリを選択イベントへ割り当てる。
4. 既存候補なら「検索して選択」→「検索」に名前／説明の一部→行をクリック。選択解除は「未設定に戻す」。
5. 「カテゴリ管理」で既存カテゴリを選び、「名前」「説明」「表示順」「使用停止」を変更→「変更を保存」。共有されている全イベントの表示に影響するがIDは変わらない。

| UI名 | 内部変数・型 | 役割・入力・候補 | 初期値／例 | 注意・根拠 |
|---|---|---|---|---|
| Category Catalog†（EventCatalog Inspector） | categoryCatalog: EventCategoryCatalogSO | Oで共有辞書を接続 | null／検証用辞書 | S04、S06.CatalogResolver。所属Catalogが複数で異なる辞書を指すと通常Inspectorで解決できない |
| Definitions†（同上） | definitions: List<EventDefinitionSO> | L＋O、ゲームで解決できる定義集合 | []／食堂・導入・後続の3件 | null／重複ID／重複CodeをS04.Initializeが検出 |
| 名前 | EventCategoryDefinition.displayName: string | 一覧名、文字 | 空／飲食店 | 空禁止。前後空白除去、FormKC＋大小文字無視で重複検査。S08.EditorValidateName |
| 説明 | description: string | 制作者向け説明、複数行 | 空／街の飲食施設 | カテゴリ検索窓の検索対象。S06.DrawCreator／DrawPicker |
| 表示順 | sortOrder: int | カテゴリ候補の並び、整数 | 0／20 | 低い順、次に表示名・ID。S08.GetCategories |
| 内部ID | id: string | GUID、読取専用 | 作成時自動 | 利用者が名称を変えても同一。S06.DrawManager |
| 使用停止 | isDeprecated: bool | 新規選択候補から除外、checkbox | false／true | 既存参照は残りWarning。既存イベントの自動停止ではない。S06／S10 |
| 使用停止を表示 | includeDeprecated: bool | 管理一覧の表示フィルタ | true | 保存されるゲーム条件ではない。S06.DrawManager |

カテゴリ管理の「使用箇所」は全ProjectのEventDefinitionSOをcategoryIdで調べる。カテゴリーそのものの物理削除UIはない。旧`EventCategory`／`EventListSO`は別の旧システムで、今回の共有カテゴリ辞書と混同しない。

## B. 発生条件

### B.1 条件Listと全入力欄

「発生条件」→`Conditions†`のL操作で追加／削除。要素の`OccurrenceCondition.query`はHideLabel＋InlinePropertyで展開される。**通常の発生条件とPointConditionは別クラス／別List**で、内部に同じEventQueryを持つ。[S03、S07]

| UI名 | 内部変数・型 | 役割・影響 | 入力・候補 | 初期値 | 設定例 | 注意点 | 根拠 |
|---|---|---|---|---|---|---|---|
| Conditions† | extension.conditions: List<OccurrenceCondition> | 全要素をAND評価 | L | [] | Night条件1つ | 空Listはtrue、null List／null要素はfalse | S13.CanAppear |
| Source† | query.source: ConditionSource | 評価対象系統 | EnumToggleButtons、7候補 | Character | World | 変更すると表示欄が変わる。過去の非表示フィールド値を消す処理はない | S03 |
| Character† | query.character: CharaData | 人物テンプレート | O | null | 任意の実在CharaData | Source Characterで必要。現在パーティーを対象 | S03／S18.Characters |
| Character instance (optional) | query.characterInstanceId: string | 特定人物個体に絞る | dropdown | 空 | Any matching character | 候補は現在SceneのPartyManager.Runtime.Members。非Play時は個体候補がない可能性。自由入力欄ではない | S03.CharacterInstances |
| Facility† | query.facility: FacilityDefinitionSO | Relation参照先 | O | null | 食堂施設定義 | 旧FacilityListSOのFacilityEntryとは別 | S03／S13 |
| Flag† | query.flag: FlagDefinitionSO | 世界状態参照先 | O | null | HeardRumor | 未保存のbool状態はfalse | S13／S15.Flag |
| Hint† | query.hint: HintData | 所有の確認対象 | O | null | RestaurantHint | Level>0をOwnedとする。Hintレベルの数値比較欄はない | S13.Evaluate |
| Event Definition† | query.eventDefinition: EventDefinitionSO | 履歴対象の定義 | O | null | 前の聞き込み | Instanceを選ぶ欄ではない | S13.Evaluate |
| History Mode† | query.historyMode: HistoryMode | 履歴のまとめ方 | Latest / Any / All | Latest | Any | EventHistory／EventParticipantで表示 | S03／S13 |
| Property (Appeal = Charm) | query.parameter: EventParameter | 人物能力 | Enum選択、下表9候補 | Appeal | Appeal | Character／EventParticipantのみ | S03 |
| Property | query.Property: string（計算プロパティ） | 現在の意味を表示 | ReadOnly | Appeal | CurrentPhase | 入力欄ではなく、自由なProperty追加も不可 | S03.Property |
| Comparison† | query.comparison: Comparison | 比較方法 | Source依存dropdown | Equal | GreaterOrEqual | 演算子の候補は下表 | S03.Operators |
| Value† | query.value: int | 人物／施設の比較値 | 整数 | 0 | 5 | 0は合法。未入力と0を区別するnullable値ではない | S03／S13 |
| Bool Value† | query.boolValue: bool | Flag／Hintの比較値 | checkbox | true | false | bool系のみ表示 | S03 |
| Phase† | query.phase: WorldPhase | 昼夜の比較値 | Day / Night | Day | Night | 経過Phase数を入力する欄ではない | S03／S13 |
| Outcome† | query.outcome: OutcomeType | 履歴結果の比較値 | SUCCESS / FAILURE / EXPIRED | SUCCESS | SUCCESS | EventHistoryのみ | S03／S13 |

| Source | 必須Target | Property | 値 | 実際の評価 |
|---|---|---|---|---|
| Character | CharaData（＋任意個体） | 選択能力 | int | 現在の同テンプレート人物のうち1人以上が成立。現在イベントにSlot投入した人物限定ではない |
| Facility | FacilityDefinitionSO | Relation | int | Progress.facilities内の指定IDのRelation。未作成は0 |
| Flag | FlagDefinitionSO | Value | bool | Progress.flags内の値。未作成はfalse |
| Hint | HintData | Owned | bool | Progress.hintsでLevel>0か |
| World | なし | CurrentPhase | Day/Night | EventManager.CurrentPhaseの偶奇 |
| EventHistory | EventDefinitionSO | Outcome | OutcomeType | Resolved=trueかつ定義ID一致の履歴を選びModeで評価 |
| EventParticipant | EventDefinitionSO | 選択能力 | int | 各対象履歴に、現在能力が条件を満たす人物が1人以上いるかをまず評価してModeへ渡す |

人物・施設の演算子：Equal（=）、NotEqual（≠）、GreaterOrEqual（≥）、Greater（>）、LessOrEqual（≤）、Less（<）。Flag／Hint／World／EventHistoryの候補はEqual／NotEqualだけ。

EventParameter：Strength→curStr、Intelligence→curInt、Appeal→curApi、Social→curSoc、Survival→curSur、Mystery→curMys、HP→currentHP、MP→currentMP、SP→currentSP。[S18.GetParameter]

### B.2 AND・履歴・判定タイミング

- Conditionsの複数行はAND。OR／NOTの条件ツリーはない。NotEqualは単独比較演算子であり、条件グループのNOTではない。
- `Latest`は対象履歴Listの最後の1件、`Any`は1件以上成立、`All`は対象全件成立。**対象0件では全Modeがfalse**。`All`の空集合をtrueにはしない。
- 履歴はInstanceごとに追記。同一定義を何度解決しても上書きしない。Latestは定義作成日時やStart順ではなく、履歴への追記順。
- EventParticipantは履歴のparticipantIdsから**現在パーティー内の人物**を探す。人物が存在しなければ評価できずfalse。全世界の人物検索・当時の能力Snapshotではない。
- 初期生成では拡張Conditionsに加え、Map Placementの旧Conditions、Unlock、背景等も検査。直接Spawn／拡張FollowUpは拡張Conditionsを検査するが、旧appearance.conditions／背景制約／generateOnMap／requiresUnlockは同じようには適用しない。
- 条件はSpawn時のチェック。StartExtendedにCanAppearの再評価はない。**夜に出現したAvailableイベントが朝まで残った場合、Night条件だけで朝のStartを禁止できない。** 夜の間だけStartさせたい例ではDeadline 1も使う。
- Phase切替は既存Availableの期限、既存Reservedの到達、予約済みFollowUpを処理する。Catalog内の全定義を毎Phase新たに自動生成するループではない。[S12.AdvancePhase、S13、S20]

### B.3 条件式の例

1. **夜に発生可能**：Source=World、Property表示=CurrentPhase、Comparison=Equal、Phase=Night。
2. **食堂と親しい**：Source=Facility、Facility=食堂定義、Comparison=GreaterOrEqual、Value=3。単に施設をマップへ配置してもRelationは増えない。
3. **前イベントを一度でも成功**：Source=EventHistory、Event Definition=聞き込みA、History Mode=Any、Outcome=SUCCESS、Comparison=Equal。履歴0ならfalse。
4. **未取得Hintだけ**：Source=Hint、Hint=RestaurantHint、Comparison=Equal、Bool Value=false。
5. **過去参加者の現在の魅力**：Source=EventParticipant、Event Definition=聞き込みA、History Mode=Latest、Property (Appeal = Charm)=Appeal、Comparison=GreaterOrEqual、Value=5。

1と2を同じConditionsへ並べれば「夜 AND Relation≥3」。1と3なら「夜 AND 聞き込みAの成功履歴あり」。

### B.4 旧EventConditionの項目（Map Placementにも露出）

| UI名 | 内部変数・型 | 役割・入力・候補 | 初期値／例 | 注意・根拠 |
|---|---|---|---|---|
| Kind† | kind: EventConditionKind | Always / JudgeSucceeded / JudgeFailed / TotalAtLeast / TotalBelow / FlagEquals / ParameterAtLeast / ResourceAtLeast | Always／ResourceAtLeast | 旧条件もAND。S02、S19.Matches |
| Parameter† | parameter: EventParameter | 旧ParameterAtLeastの能力 | Strength／Appeal | 他Kindでは使わない |
| Key† | key: string | 旧Flag／Resource識別子 | 空／Gold | 新FlagDefinitionのID自動接続ではない |
| Value† | value: int | Total／Parameter／Resourceの閾値 | 0／100 | TotalBelowは`<`、他AtLeastは`>=` |
| Flag Value† | flagValue: bool | 旧FlagEquals値 | true／false | 外部IEventWorldState依存 |

初期配置はinstance／cardsのないContextで旧条件を評価するため、判定後条件や割当能力条件を入れない。旧Validatorはその配置をErrorにするが、拡張Validatorへの分岐後は旧appearance.conditionsの同じ検査を実行しない点にも注意。[S10、S11、S19、S20]

## C. Slot設定

「Slot設定」→Slots†にL操作で枠を追加し、人物・施設・装備等の受入型を決める。ゲーム内で実際のカードをドラッグして配置する操作と、Editorで枠を作る操作は別。

| UI名 | 内部変数・型 | 役割・ゲームへの影響 | 入力・候補 | 初期値 | 設定例 | 注意点 | 根拠 |
|---|---|---|---|---|---|---|---|
| Slots† | reservation.cardSlots: List<CardSlotDefinition> | Slot順と数 | L | [] | 人物枠1つ | Validator上限32。並べ替えはChoiceの格納位置にも影響 | S02／S07／S12 |
| Label† | slot.label: string | 枠表示名 | 文字 | 空 | 聞き込み担当 | 空ならCardType名がゲーム内枠名 | S23.Show |
| Slot Type† | slot.slotType: SlotType | 必須／任意／選択／自動 | Required / Optional / Choice / Involved | Required | Required | 新方式で使用 | S03／S12 |
| Involved Target† | slot.involvedTarget: UnityEngine.Object | 自動参加対象 | O | null | 特定CharaData | Involvedで表示。型は汎用ObjectだがValidatorはCharaData／FacilityDefinitionSO／EquipDataのみ許容 | S03／S11 |
| Accepted Type† | slot.acceptedType: CardType | 受け付けるカード型 | Chara / Skill / Equip / Money / Facility / Item | Chara | Chara | フィールドだけで全型のカード供給UIが完成するわけではない | S25／S12 |
| Required† | slot.required: bool | 従来方式の必須フラグ | checkbox | true | 旧イベントの任意枠ならfalse | **拡張では無視**。Required=falseでもSlot Type=Requiredなら必須 | S02.Tooltip／S12 |
| Amount† | slot.amount: int | カード数量の最低条件 | 正整数 | 1 | 1 | 人物／施設はValidatorで1のみ。配置検査でありItemを報酬から引くEffectではない | S11／S12 |
| Requirements† | reservation.requirements: List<EventRequirement> | 全割当人物の能力合計のStart条件 | L | [] | Appeal≥5 | 各Slot個別の能力条件ではない | S12.ValidateCards |
| Parameter†（Requirement内） | requirement.parameter: EventParameter | 合計能力 | 9候補 | Strength | Appeal | 成功判定Parameterとは別設定 | S02／S18 |
| Minimum† | requirement.minimum: int | Startを許可する下限 | 0以上整数 | 0 | 5 | 未達はFAILUREに解決するのでなくStart拒否 | S12.ValidateCards |

| Slot Type | Start時の挙動 | UIと制約 |
|---|---|---|
| Required | 有効なカード必須 | 型・数量・重複使用を検査 |
| Optional | 空でも可 | 入れた人物は成功判定の能力合計にも含まれる |
| Choice | 空でも可 | カードIDをSlot順で保存。Choice専用の分岐条件・Outcome選択UIはない |
| Involved | 対象に合うカードIDを自動補完 | EventAutomaticにより手動投入不可。対象名を表示するが、カードを枠へ自動で見た目上移動する処理ではない |

**人物の指定**：通常Required枠にはCharaData指定欄がない。誰を参加させるかはプレイヤーのカード投入で決まる。Involvedは指定テンプレートに合い、EventLockedでないカードをID順で最初の1枚選ぶ。EventQueryのCharacter欄は発生条件であり、Slotの参加者制限ではない。[S18.FindCard]

**参加者確定**：Start（ReservationController.Confirm→Reserve→StartExtended）でassignedCardIdsを保存し、カードから対応するCharacterRuntimeData.runtimeIdを重複除去してparticipantIdsへ記録する。施設・装備IDを人物履歴に混ぜない。同じカードIDの二重使用やReserved／Ready／Runningで使用中のカードは拒否。能力評価は現在値、参加者IDの確定だけがStart時である。[S12、S13、S18]

旧派遣コードの`hakenCan`と新方式のカードロックは同じ条件ではない。新ValidateCardsにhakenCanの検査はない。人物パレット生成・CharaDataの作成・個体ID復元は既存の人物システム側の前提になる。

## D. 時間設定

| UI名 | 内部変数・型 | 役割・ゲームへの影響 | 入力・候補 | 初期値 | 例 | 注意 | 根拠 |
|---|---|---|---|---|---|---|---|
| Deadline Phase Count† | extension.deadlinePhaseCount: int | 出現からStartするまでの期限 | 0以上整数 | 2 | 1 | 0は無期限ではなく即期限切れ | S07／S12.Spawn／S13.Expire |
| Duration Phase Count† | extension.durationPhaseCount: int | Startから解決待ちまでの時間 | 0以上整数 | 1 | 1 | 0でもStart操作と結果表示／続行は必要 | S13.StartExtended／S14 |
| Delay Phase Count†（FollowUps内） | followUp.delayPhaseCount: int | 親結果から後続出現までの遅延 | 0以上整数 | 0 | 1 | 「時間設定」にはなくOutcomeのFollowUps内 | S02／S13.ScheduleFollowUp |
| Allow Concurrent†（同上） | followUp.allowConcurrent: bool | 同一定義の重複予約・出現の許可 | checkbox | false | false | 当該FollowUpの重複規則。全てのSpawnに対する世界共通禁止設定ではない | S13 |
| Trigger Mode†（旧予約タブ） | reservation.triggerMode: EventTriggerMode | 従来のScheduled／Immediate | Enum | Scheduled | Scheduled | 拡張ではDurationを使用。旧ImmediateとDuration 0を混同しない | S12 |
| Required Phase Count†（旧予約タブ） | reservation.requiredPhaseCount: int | 従来の予約時間 | 非負整数 | 1 | 2 | 旧Scheduledは>0、旧Immediateは0かつカード／Requirementsなし | S10／S12 |

### D.1 Phaseと期限の境界

`EventManager.CurrentPhase = (日付−1)×2 + (Nightなら1、Dayなら0)`。1日目昼=0、夜=1、2日目昼=2、夜=3。1 Phaseはゲーム内の1時間帯で、1秒や1移動ではない。TimeManagerは1時間帯に25の行動時間を持ち、有効な移動でConsumeTime(1)、0になると昼夜が切り替わる。入力待ちだけでリアルタイムに減るタイマーはこの処理にはない。[S21.CurrentPhase、S26]

出現p、Deadline=dなら、Availableのまま`CurrentPhase >= p+d`になった時点で期限切れ。締切と同じPhaseになってからStartする猶予はない。Start済みReservedにはDeadlineを適用しない。Start=s、Duration=tなら`executePhase=s+t`でReadyになるが、結果処理はRunnerとプレイヤーの「続行」による。

| 時点 | 例：夜p=1に出現、Deadline=1、Duration=1、成功後Delay=1 |
|---|---|
| 1日目夜／p=1 | Available。夜のうちにStartすると人物固定・Reserved、executePhase=2 |
| 2日目昼／p=2 | Start済みならReady→Running。成功表示後の続行でEffects→履歴→後続予約due=3 |
| 2日目夜／p=3 | 空きセルと後続Conditionsが成立すれば後続がAvailableとして出現 |
| 別分岐：p=1でStartしなかった | p=2でEXPIRED。participantIdsは空。SUCCESS／FAILUREのダイス判定はしない |

EXPIREDは「結果名を確定してReadyにする」までが期限処理。その後Runnerが効果を適用し履歴を記録する。別イベントがRunningなら待つ。期限切れ専用Presentationは現在のPumpでスキップされる。[S13.Expire、S14.Pump]

### D.2 2種類の「予約」・重複・取消

- **参加予約**：Start済みEventInstanceがReserved。Slotカードを確保する。
- **後続の出現予約**：Progress.followUps内のScheduledFollowUp。まだEventInstanceはない。Start用カードも未確保。
- allowConcurrent=falseの予約作成時は、同一定義の他Available／Reserved／Ready／Running、または既存FollowUp予約があれば抑止する。解決中の親自身は除外されるので自己FollowUpで繰返し可能。
- allowConcurrent=trueなら独立した予約／Instanceを追加できる。ただしセル容量・条件等を無視しない。
- due到達後も条件未成立・対象無効・空きセルなしなら予約を保持。次のPhaseやイベント解決時等に再試行。非Concurrentで他の同一定義が出現していた場合は、出現を抑止してその予約を取り除く。
- 予約一覧UIは**Start済みInstanceのみ**。将来のFollowUp予約を表示／取消する専用UIはない。[S21.RefreshSchedule]
- 予約一覧の取消はReserved／ReadyをCancelledにし、カード・セルを解放する。Availableへ戻す操作ではなく、EXPIRED報酬や履歴も追加しない。Runningは取り消せない。[S12.CancelReservation、S24]

## E. HintLevel設定

| UI名 | 内部変数・型 | 役割・ゲームへの影響 | 入力・候補 | 初期値 | 例 | 注意 | 根拠 |
|---|---|---|---|---|---|---|---|
| Point Conditions† | extension.pointConditions: List<PointCondition> | 成立行のポイントを個別加算 | L＋B章の全Query欄 | [] | Nightなら+2 | 発生条件と別。1行不成立でも他の加点は生きる | S03／S13.CalculateHintLevel |
| Add Point† | PointCondition.addPoint: int | その行の加算値 | 整数 | 1 | 2 | 非負制限はなく、0・負値も設定可能 | S03／S11 |
| Level 2 Points† | extension.level2Points: int | Lv2の最低点 | 整数 | 2 | 2 | Hint Effectごとの固定Levelではない | S07／S13 |
| Level 3 Points† | extension.level3Points: int | Lv3の最低点 | 整数 | 4 | 4 | Lv2未満ならError。同値なら先にLv3となりLv2区間がなくなる | S11／S13 |
| Case Hint Pool† | extension.randomHintPool: HintPoolSO | 非固定Hintの抽選候補 | O | null | RestaurantCasePool | 現行はイベントごとの参照。自動で現在事件から取得する仕組みではない | S07／S15.GrantHint |

計算時点は**結果Effect適用を開始するApplyNextの最初**。一度計算したhintPoints／hintLevelをInstanceに保存し、途中再開で再計算しない。開始時や出現時ではない。通常のSUCCESS／FAILUREでは結果表示を経て続行した時点となる。全成立点を合計し、`>=Level3Points → Lv3`、次に`>=Level2Points → Lv2`、その他Lv1。空リスト・0点でもLv1であって「Hintを付与しない」意味ではない。[S13、S14]

**例**：PointCondition 1=World Equal Nightで+2、2=Facility 食堂 Relation GreaterOrEqual 3で+2、閾値2／4。夜かつ親しいなら4点→Lv3、片方だけ2点→Lv2、両方不成立0点→Lv1。夜にStartしてDuration 1で翌朝に解決するなら、Nightの+2は通常成立しない。夜の加点を期待する例はDuration 0等で解決時点も夜にするか、別のPoint条件を使う。

Character条件は現在パーティーの人物を見ており、Optional Slotへ入れた人物限定ではない。Optionalを入れたこと自体を選ぶSource／Propertyは未実装。既存の成功判定合計にはOptionalも参加するが、これを「Optional投入で必ずHintLevelが増える」と説明しない。

### E.1 Hint・Pool・Flag・Facility参照アセットの全項目

| UI名 | 内部変数・型 | 役割・入力・候補 | 初期値 | 例 | 注意・ゲーム影響・根拠 |
|---|---|---|---|---|---|
| Persistent Id† | EventIdentitySO.persistentId: string | 自動GUID、ReadOnly指定 | 自動 | 自動値 | Hint／Pool／Flag／Facility共通。実InspectorでのReadOnly描画は未確認。S29 |
| Display Name† | displayName: string | 文字入力、表示用 | 空 | 店主の証言 | Projectファイル名と別。条件Object欄は通常アセット名表示になる。S29 |
| Description† | description: string | 複数行、説明 | 空 | 食堂で得た情報 | Hintの音楽的内容への変換定義ではない。S29 |
| Scope†（Flagのみ） | FlagDefinitionSO.scope: FlagScope | Global / CaseのEnum | Global | Case | 現在の状態キーはIDだけ。Scopeによる自動初期化・分離処理はない。S03／S15 |
| Hints†（Poolのみ） | HintPoolSO.hints: List<HintData> | L＋O、候補の共有集合 | [] | HintA、HintB | null除外・ID重複除去して抽選。施設・人物の型は入れられない。S15.Draw |

固定／ランダムの選択はこのグループでなく、F章の**HintEffect.Is Fixed Hint†**。trueならFixed Hint†、falseなら上記Case Hint Poolを利用。同一PoolのID単位で一巡まで既出を除外し、全候補を出し切った後の次回抽選で抽選履歴だけをクリアする。所有Hintは消えない。同じHintの新Levelが高ければ更新し、同じ／低いなら維持。Levelは1～3に丸められる。[S15.Draw／GrantHint]

`HintData`にはmove／density等の音楽要素やLv別文章テンプレート欄はない。`HintSelectionState`（`Emo5Toggle.cs`）と`AnswerSet`は別系統。楽曲選択はMove／Density／Order／Rhythmと感情の状態から非Noneを最大3項目に制約し、MaxEmo／MinEmoは排他。**所持したHintDataを自動でその選択UIへ対応させるコードは確認できない。** 第5音楽要素の編集UIもない。[S31]

## F. Outcome／Effect／FollowUp設定

### F.1 結果の編集・適用順

「Outcome設定」のSUCCESS／FAILURE／EXPIREDを選ぶ。内部は`extension.success`／`failure`／`expired: EventOutcomeDefinition`で、固定の3セクション。SUCCESS／FAILUREはA.3の判定式、EXPIREDはStart前の期限で決まる。拡張専用画面には自由なOutcome追加、Outcome Code変更、結果ごとの条件式、デフォルト結果の選択欄はない。

| UI名 | 内部変数・型 | 役割・影響 | 入力・候補 | 初期値 | 例 | 注意 | 根拠 |
|---|---|---|---|---|---|---|---|
| Presentation† | extension.<結果>.presentation | 結果文章・画像 | Text＋Image | 空／null | 「報酬を得た」 | EXPIRED表示の制限はA.3参照 | S07.OutcomeEditor／S14 |
| Effects (+ Effect追加) | extension.<結果>.typedEffects: List<OutcomeEffect> | 異種効果を順番に適用 | L＋具象型選択 | [] | GoldEffect、HintEffect | **この文字列はListのLabelText。独自の同名Buttonメソッドはない**。Odinの型選択ポップアップの実配置は未確認 | S07／S02.SerializeReference |
| Follow Ups† | extension.<結果>.followUps: List<EventFollowUp> | 結果後の発生予約 | L＋定義O等 | [] | AfterRestaurant | Effectsとは別List | S07／S13 |

実行順は、旧`effects`（もし存在すれば）→`typedEffects`をList順→履歴追記→FollowUpsをList順に予約→親Resolved・セル解放→到達済み後続を出現。**複数Effectは一括トランザクションではない。** 途中で失敗すると適用済み分は戻さず、カーソルを維持して失敗箇所を再試行する。Gold付与後に不正Hintで止まる、といった部分適用が起こり得る。[S14.ApplyNext、S17]

### F.2 共通Effect 5種類の全入力

| 種類／UI名 | 内部変数・型 | 役割・ゲームへの影響 | 入力・候補 | 初期値 | 設定例 | 注意点 | 根拠 |
|---|---|---|---|---|---|---|---|
| GoldEffect / Amount† | GoldEffect.amount: int | 正なら所持金増加、負なら支払い | 整数 | 1 | 100 | 負数の支払不足は処理失敗。int上限超過も拒否 | S03.Apply／S17.TryApply |
| ItemEffect / Item† | ItemEffect.item: EquipData | 装備在庫へ指定データ付与 | O | null | 実在EquipData | 一般消費アイテム型ではなく既存EquipData | S03／S17 |
| ItemEffect / Amount† | amount: int | 付与数 | 正整数 | 1 | 2 | 1～10000を検査 | S11／S17 |
| HintEffect / Is Fixed Hint† | isFixedHint: bool | 固定／ランダム切替 | checkbox | true | false | false時はFixed Hint欄が非表示 | S03 |
| HintEffect / Fixed Hint† | fixedHint: HintData | 固定付与先 | O | null | RestaurantHint | trueなら必須。falseではこの値を使わない | S11／S15 |
| RelationEffect / Facility† | facility: FacilityDefinitionSO | Relation更新先 | O | null | 食堂施設定義 | 人物の友好度更新ではない | S03／S15 |
| RelationEffect / Amount† | amount: int | Relation増減 | 整数 | 1 | 1／-1 | 現行は負値も許容、int overflowは拒否。初期Relation=0 | S15.AddRelation |
| FlagEffect / Flag† | flag: FlagDefinitionSO | bool状態更新先 | O | null | HeardRestaurantRumor | nullはError | S03／S11 |
| FlagEffect / Value† | value: bool | 状態値 | checkbox | true | true | Flag定義アセットにboolを書き込まずProgress.flagsを更新 | S15.SetFlag |

追加型の一覧としてコードから確認できる具象型はこの5つ。異なる型を同じEffectsに混在できる設計。`OutcomeEffect`派生型を増やすコード変更をすれば拡張可能だが、初心者がEnumへ自由な効果名を追加するUIではない。

**成功例**：SUCCESSのEffectsへ①GoldEffect Amount=100、②HintEffect Is Fixed Hint=true／Fixed Hint=RestaurantHint、③FlagEffect Flag=HeardRestaurantRumor／Value=trueを順に追加。これは所持金+100、算出済みHintLevelでHint付与、Flag=true。ポイントが低くても①と③は通常どおり適用される。FAILUREのEffectsを空にすればその結果では何も付与しない。

### F.3 FollowUpの全入力

| UI名 | 内部変数・型 | 役割・ゲームへの影響 | 入力・候補 | 初期値 | 設定例 | 注意点 | 根拠 |
|---|---|---|---|---|---|---|---|
| Definition† | EventFollowUp.definition: EventDefinitionSO | 後続の定義 | O | null | AfterRestaurant | 同じ実行Catalogに登録しenabled=trueにする。自己参照も可能だが循環に注意 | S11／S13 |
| Start Mode† | startMode: EventFollowUpMode | 旧方式の動作 | Unlock / SpawnAvailable / Reserve | Unlock | 拡張では変更不要 | **拡張FollowUpでは無視される**。表示されてもUnlockになるわけではない | S02.Tooltip／S14.ApplyNext |
| Delay Phase Count† | delayPhaseCount: int | 拡張の後続出現遅延 | 0以上整数 | 0 | 1 | 旧方式のStartFollowUpはこの遅延値を使わない | S12／S13 |
| Allow Concurrent† | allowConcurrent: bool | 拡張の重複許可 | checkbox | false | false | 旧方式ではこのフラグを参照しない | S12／S13 |

追加／削除はFollow UpsのL操作。後続のDefinition参照をセットしただけでは登録されない。後続自身の条件が不成立ならdue到達後も保留される。出現はAvailableなので、その後プレイヤーがStartする。後続がDeadline 0なら出現直後にEXPIREDになる。自動的に人物カードを割り当てて開始する新FollowUp機能はない。

### F.4 従来Outcomeと旧Effect（混同防止のための全項目）

旧「結果」タブの`outcomes: List<EventOutcomeDefinition>`初期値はSUCCESS（JudgeSucceeded条件）、FAILURE（isDefault=true）の2件。拡張の3結果と別。旧結果では非Defaultの条件を順に調べ、最初の成立結果、なければDefaultを使用する。

| UI名 | 内部名・型 | 役割・入力・初期値／例 | 注意・根拠 |
|---|---|---|---|
| Outcome Code† | outcomeCode: string | 旧結果の識別文字、初期SUCCESS／FAILURE、例SUCCESS | 旧では任意文字列、拡張では固定名が必要。S02／S10／S14 |
| Is Default† | isDefault: bool | 旧のフォールバック結果。新規要素false、初期FAILUREのみtrue | 旧Validatorはちょうど1件を要求。拡張は無視。S10 |
| Conditions† | conditions: List<EventCondition> | 旧結果のAND条件、初期は結果による。B.4の全欄 | 拡張のSUCCESS／FAILURE選択には使わない。S14 |
| Presentation† | presentation: EventPresentation | Text／Image、空／null | 旧検査は文章・画像欠落をError、新方式は基準が異なる。S10／S11 |
| Effects† | effects: List<EventEffect> | 旧共用構造の効果List、[] | 拡張結果内部に存在すると先に実行されるが専用画面に欄なし。S17 |
| Typed Effects† | typedEffects: List<OutcomeEffect> | 型付き効果List、[] | 新方式の「Effects」はこのフィールド。旧画面の自動表示形は未確認 |
| Follow Ups† | followUps: List<EventFollowUp> | 後続、[] | 旧方式ではstartMode、新方式ではdelay／concurrent。S12／S13 |

旧EventEffectは、`Kind†/kind: EventEffectKind`（初期GrantGold）、`Amount†/amount:int=1`、`Parameter†/parameter:EventParameter=Strength`、`Key†/key:string=空`、`Flag Value†/flagValue:bool=false`、`Ability†/ability:AbilityData=null`、`Equip†/equip:EquipData=null`、`Hint†/hint:HintData=null`、`Event Definition†/eventDefinition:EventDefinitionSO=null`の全フィールドを持つ。文字は入力、数値は整数、enumは選択、アセットはO。旧構造はKindに応じた全欄のShowIf指定がなく、無関係な欄も見える可能性がある。

| 旧Kind | 有効欄・例・影響 | 注意 |
|---|---|---|
| GrantGold / ConsumeGold | amount=100。所持金付与／支払 | 旧はamountを非負で入力 |
| GrantAbility | ability＋amount=1。Abilitiesへ付与 | AbilityData必須、1～10000 |
| GrantEquip | equip＋amount=1。Equipsへ付与 | EquipData必須、1～10000 |
| GrantHint | hint＋amount=1。旧Hintsリストへ追加、安定IDがあれば所有Lv1も反映 | 旧リストへの複数追加と新Hintレベル更新を混同しない |
| ChangeParameter | parameter=Appeal、amount=1。割当人物の現在能力を変更 | 人物がいないと失敗 |
| SetFlag | key＋flagValue。外部IEventWorldStateへ委譲 | 新FlagDefinitionSO状態とは自動連動しない。Scene.worldState未設定 |
| UnlockEvent | eventDefinition。アンロック集合に登録 | 単独ではマップへSpawnしない |
| ReserveEvent | eventDefinition。旧StartFollowUpのReserveへ | カード不要等の旧自動予約制約 |
| StartBattle | key。Encounterハンドラへ委譲 | 接続ハンドラがなければ失敗 |

根拠：S02.EventEffect、S17.TryApply、`Assets/Script/Events/Integration/PartyEventWorldState.cs`。

### F.5 履歴とInstance・セーブの正式構造

履歴の編集・閲覧専用ウィンドウは確認できない。これらは制作時設定ではなく、プレイ結果のデータである。

| データ | 主な内部項目・型 | 意味・初期状態・注意 | 根拠 |
|---|---|---|---|
| EventHistoryRecord | definitionId:string、instanceId:string、resolved:bool、outcome:OutcomeType、participantIds:List<string> | 解決した定義と発生を区別。RecordHistoryはresolved=trueで追記。EXPIREDの参加者は空。定義ごとでなくInstanceごとに一意 | S15／S13.RecordHistory |
| EventInstanceの識別・配置 | instanceId、definitionId:string／status:EventStatus／cellPosition:Vector2Int／occupiesCell:bool | Spawnで新GUID、Available、セル占有。状態候補はAvailable, Reserved, Ready, Running, Resolved, Cancelled | S16／S12.Spawn |
| EventInstanceの時間 | appearedPhase:int／deadlinePhase:int=-1／reservedPhase:int=-1／executePhase:int=-1 | 拡張の出現・期限・Start・完了待ちPhase。-1は未設定／従来期限なし | S16／S12／S13 |
| EventInstanceの参加 | assignedCardIds、participantIds:List<string> | 前者はSlot順のカードID、後者は重複除去された人物ID。初期[] | S13.StartExtended／S18 |
| EventInstanceの判定 | randomSeed:int／resolvedOutcomeCode:string／hasJudgement:bool／diceResults:List<int>／judgeTotal:int／judgeSucceeded:bool | seedはSpawnで設定。初期未判定。結果を再利用して二重判定を避ける | S14／S16 |
| EventInstanceのHint | hintLevel:int=1／hintPoints:int=0／hintLevelCalculated:bool=false | 解決時に一度計算。参加時の能力Snapshotではない | S13.CalculateHintLevel |
| EventInstanceの再開 | appliedEffectCount、appliedFollowUpCount:int=0／immediateAncestors:List<string>／lastError:string | 適用済み処理位置・即時循環保護・失敗情報 | S14／S16 |
| EventProgress | history、flags、hints、draws、facilities、followUps:各List | 全て初期[]。Flag=false、Relation=0、Hint未所有は照会時の既定 | S15 |
| ScheduledFollowUp | id、definitionId、parentId:string／duePhase、scheduledPhase:int／allowConcurrent:bool／preferredCell:Vector2Int | 将来の出現予約。未出現時点では独立Instance IDをまだ持たない | S15／S13.ScheduleFollowUp |
| EventSaveData | version:int（型の初期値1）、progress:EventProgress、currentPhase:int、instances、unlockedDefinitionIds:List | CreateSaveDataがversion=2を書き出す。version 1／2の読込を実装 | S16／S12.CreateSaveData／Restore |

Effects完了後、FollowUps処理前に履歴を記録する。FollowUpの設定不備で親がRunningのまま停止しても、履歴にはresolved=trueが存在し得る。キャンセルは履歴のOutcomeとして定義されていない。イベント状態DTOは存在するが、全ゲームの保存ファイル選択・保存／ロードボタンはこの編集UIにない。人物runtimeId・カード・Time・所持金等の復元をEventSaveDataだけで完結させる設計ではない。

## G. Validator

### G.1 起動経路・表示・更新

1. **個別表示**：拡張Inspectorの「Validator」を開く。S07.Validationが描画時に`EventExpansionValidator.Validate`を呼ぶ。専用実行ボタンはなく、拡張部分の指摘を自動表示。
2. **全体検査**：Databaseの「全イベント検査」を押す。S05.ValidateAllがProject内の全EventDefinitionSOを再取得してS10.Validateへ渡す。指摘横の「開く」で対象へ移動、修正後に再度検査する。

| 項目 | 個別Validator | Database全体検査 |
|---|---|---|
| 範囲 | 選択定義の拡張設定 | 全定義、選択Catalog、カテゴリ、旧／新検査、参照循環 |
| Catalog | Databaseから渡す、なければ登録先検索の最初のCatalog | ユーザーが選んだCatalog |
| 色・重大度 | Error／Warning／Infoを正しくHelpBoxへ分けるコード | **Error以外はWarningアイコンで描画**。Infoでも本文にはInfoと出る |
| 件数 | 専用集計なし | Errors／Warningsのみ集計しInfoは含めない |
| 更新 | Inspector描画時 | 「全イベント検査」押下時 |
| 編集／実行禁止 | なし | なし |

個別で何も出ない＝カテゴリやCodeを含めすべて正常、ではない。Database一覧／検査は選択Catalog内限定ではないので、別Catalogのテスト用定義も指摘される。複数Catalogを使う構成では「未登録」「カテゴリID不明」「後続未登録」が、どのCatalogを選択して検査した結果か確認する。

### G.2 チェック内容・修正の方向

| 重大度・対象 | 条件と主なメッセージ | 修正例・ゲームへの影響 | 根拠 |
|---|---|---|---|
| Error：全体ID／Code | GUID不正、Code／表示名空、重複GUID／Code | Code補完／一意化。ID重複は正しい複製経路を使う。Catalogは曖昧なキーを除外 | S10／S04 |
| Error：Catalog／カテゴリ | Catalog未指定、辞書未設定・不正、Enabledのカテゴリ未設定、未知ID | Catalogと辞書を接続、共有カテゴリ選択。カテゴリErrorは必ずしもSpawn全停止を意味しない | S10.CheckCategory |
| Warning：非対象Catalog等 | 選択Catalog未登録、使用停止カテゴリ、無効イベントのカテゴリ未設定、移行待ち | 登録先／対象を確認、必要なら割当・移行 | S10 |
| Error：拡張時間 | 負のDeadline／Duration | 0以上にする。int加算overflowは実行時にも拒否 | S11／S12／S13 |
| Info：Deadline 0 | `Deadline 0 resolves EXPIRED without Start or participants.` | 意図した即時期限なら合法。Startさせるなら1以上 | S11 |
| Error：HintLevel | Level3閾値 < Level2閾値 | 閾値の順を直す | S11 |
| Error：条件／Point条件 | null、必要Target欠落、不正なSource／Comparison／HistoryMode、bool等に不適切な演算子、イベント参照未登録 | Sourceに対応する参照と値を設定。0自体は「値欠落」ではない | S11.Query |
| Error：Slot | null、32超、amount<1、不正enum、人物／施設amount≠1、Involved対象欠落・不適切型・型不一致 | 枠数、Amount、対象とAccepted Typeを合わせる | S11 |
| Error：実行 | 拡張でNarrative以外、Judge未設定、Dice Count 1～100／Sides 2～10000外 | Narrativeと適正Dice値へ。UseDice=falseでも検査対象 | S11 |
| Error：Outcome | 3結果が欠落／改名、Presentationまたはリストnull | 正規の3結果とデータ構造へ戻す。破損参照は専用UIだけで修復できない場合あり | S11 |
| Warning：文章／アイコン | 新方式結果の文章空、マップアイコンなし | 文章／Spriteを補う。処理自体は必ずしも停止しない | S11 |
| Error：Effect | null型、Flag／Facility欠落、Item資産・数量不正、固定Hint未指定、ランダムPoolがnull／空 | 必須アセット選択と数量補正 | S11 |
| Info：旧Effect残存 | `Legacy effects remain and execute before typed Effects.` | 隠れた旧effectsが先に動くことを確認。専用画面では旧Listを直接編集できない | S11 |
| Error：FollowUp | null／未登録定義、負のDelay、Deadline 0＋Delay 0の自己循環 | 参照先登録、Delay補正、無限連鎖解消 | S11 |
| Warning／Error：循環 | 一般参照ループはWarning、危険な自動Immediate循環はError | 人のStartを挟む意図した反復と区別して確認 | S10.HasCycle |
| Error：旧方式固有 | Scheduled時間≤0、Immediate時間≠0・カード要求、Default結果件数≠1、文章／画像欠落など | 従来設定の仕様に合わせる。新方式の画像任意等と混同しない | S10 |

**検査が網羅しない例**：現在所持金で支払えるか、参照型資産のGUID重複・空ID全般、人物／施設カードがSceneに存在するか、外部ハンドラ接続、負のGoldの実行可否、拡張側の旧appearance.conditionsやrequirementsの旧検査すべて、Source変更後の非表示の不要eventDefinitionが残る問題。Query検査はSourceに関係なく非nullのeventDefinitionの登録を検査するため、Sourceを変更しても隠れた旧参照がErrorの原因になり得る。

エラー残存時は、Spawnで拒否、Start拒否、結果処理の停止、カードが確保されたまま、マップにアイコンがない、Consoleへの出力等があり得る。Validatorを通しただけで実機動作を保証しない。制作データを修正後、Playをやり直して初期状態から確認するのが安全であり、進行中のEffect順序を変更して再開する手順を初心者へ推奨しない。

## 3. 具体的な制作手順：夜の飲食店 → 報酬 → 後続予約

### 3.1 この例の前提と完成形

これは**今後人が操作するための設定例であり、今回アセットを作成・変更したものではない**。まずテスト用CatalogとSceneで確認する。起動時は昼であり、Night条件の定義をCatalogへ登録するだけでは夜に自動配置されない。このため、既存のUIとFollowUpで構成できる、次の3イベントを使う。

```text
A：導入（初期マップ配置、昼にStart、Duration 0）
  └ SUCCESS FollowUp：R、Delay 1
      R：夜の飲食店（Night条件、Duration 1）
        └ SUCCESS：Gold +100、固定Hint付与
            └ FollowUp：B、Delay 1
                B：聞き込みの続き（条件なし、Duration 0）
```

**「カテゴリが飲食店」と「飲食店の背景セル」と「FacilityのRelation」は独立した概念**。この例の飲食店はイベント名・カテゴリ・文章による表現であり、FollowUpは特定の飲食店背景へ配置する保証を持たない。背景を指定した夜の自動配置を要件とするなら追加実装が必要で、この操作例で達成したことにしない。

事前に `Assets > Create > Events > Event Catalog` でテストCatalogを作成し、選択して `Tools > Events > Create Category Catalog and Connect` を実行する。DatabaseのCatalog欄へそのCatalogを設定する。既存の本番Catalogへ登録しながら説明する場合は、混在する既存データの影響を別途確認する。

参照データとして `Assets > Create > Events > Hint` からHintDataを作成し、表示名を「飲食店で得た噂」、説明を「次の聞き込みにつながる情報」にする。固定Hintを使うのでCase Hint Poolは未設定でよい。画像／マップアイコンに使える既存Spriteと、プレイ中に参加させられる人物カードも必要。

| 設定 | A：導入 | R：夜の飲食店 | B：聞き込みの続き |
|---|---|---|---|
| Event Code | `DINING_INTRO` | `DINING_NIGHT` | `DINING_NEXT` |
| Display Name | 飲食店へ行く準備 | 夜の飲食店 | 聞き込みの続き |
| 共通 | Enabled=true、拡張ライフサイクル=true、Execution=Narrative、カテゴリ「飲食店」、適切なSprite | 同左 | 同左 |
| Generate On Map | true | false | false |
| Required Placements | 1 | 0 | 0 |
| 背景制限／旧Appearance Conditions | 空 | 空 | 空 |
| 発生Conditions | 空 | World / Equal / Nightを1件 | 空 |
| Slot | 空 | Required / Chara / Amount 1を1件 | 空 |
| Requirements | 空 | 空 | 空 |
| Deadline / Duration | 4 / 0 | 1 / 1 | 2 / 0 |
| Judge | Use Dice=false、Target=0、Modifier=0 | Use Dice=false、Parameter=Appeal、Target=5、Modifier=0 | Aと同じ |
| Point Conditions | 空 | World / Equal / Day、Add Point=4 | 空 |
| Level 2 / Level 3 | 2 / 4 | 2 / 4 | 2 / 4 |
| SUCCESS Effects | 空 | GoldEffect Amount=100、HintEffect固定で上記Hint | 空 |
| SUCCESS FollowUps | R、Delay Phase Count=1、Allow Concurrent=false | B、Delay Phase Count=1、Allow Concurrent=false | 空 |
| FAILURE / EXPIRED | 文章のみ、EffectsとFollowUpsは空 | 同左 | 同左 |

AとBは参加者なし・Diceなし・Target 0なので通常は合計0でSUCCESSになる。Rは参加人物の解決時Appealが5以上なら成功、未満なら失敗。カードの能力値を確認し、成功を確認する回と失敗を確認する回を分ける。人物が誰でも必ず成功すると説明しない。Rを開始せず次の朝まで置けばEXPIREDの確認になる。

### 3.2 13段階の操作

以下の†・L・Oは0章の表示確度／標準操作の定義に従う。リスト追加の実際のポップアップ名など、コードに明示されていない文字列は断定しない。

| 手順 | 操作する画面・グループ | 押すボタン／変更するフィールド・入力値 | 操作後に起こること | 注意点 | 対応コード |
|---|---|---|---|---|---|
| 1. 開く | Unityメニュー→Database「カタログ」 | `Tools > Events > Event Database`。CatalogへテストCatalogをOで指定 | 一覧と作成・検査UIが開く | 一覧には他Catalogの定義も出る。新規作成先と実行時Catalogの両方を確認 | S05.Open／Refresh／DrawList |
| 2. 新規作成 | Database「編集」 | 「新規イベント」で保存先と名前を指定し、A・R・Bの3定義を作る。それぞれExtensionタブのUse Extended Lifecycle†をONにする | GUID生成。Catalog指定済みなら登録。拡張7グループへ切り替わる | 切替前の自動名はUse Extended Lifecycle†。一方の方式のOutcome設定が他方へ移植されるわけではない | S05.CreateAsset／RegisterSelected、S06.OnInspectorGUI |
| 3. IDと名前 | カテゴリヘッダー／基本情報 | Rを選択。Event Code†=`DINING_NIGHT`、Display Name†=夜の飲食店、Enabled†=true。「新規追加」でカテゴリ飲食店を作成／既存から選択。A・Bも上表のCodeと名前にする | Codeで人が識別、内部GUIDで参照。全定義にカテゴリが付く | Definition Id†は自動生成の読取専用ID。手で書き換えない。Codeを重複させない | S01、S05.UniqueCode、S06カテゴリSelector／Create |
| 4. 夜の出現条件 | R「発生条件」／基本情報→Map Placement† | Conditions†にLで1要素追加。Source†=World、Comparison†=Equal、Phase†=Night。Map PlacementのGenerate On Map†=false。Aはtrue・Required Placements†=1 | Rは夜にSpawn可能。Aだけが起動時配置候補になる | 条件だけで夜の自動Spawnはしない。AのFollowUpで出現契機を作る。Aの背景制限は空 | S03.EventQuery、S13.CanAppear、S20.Generate |
| 5. 参加Slot | R「Slot設定」 | Slots†にLで1要素。Label†=参加者、Slot Type†=Required、Accepted Type†=Chara、Amount†=1。Requirements†は空 | プレイ時に人物カード1枚を必要とする | Requiredという旧boolを操作するだけでは新Slot Typeの代わりにならない。A・BのSlotsは空 | S02.CardSlotDefinition、S13.TryBuildAssignments／StartExtended、S23 |
| 6. 時間・期限 | R「時間設定」 | Deadline Phase Count†=1、Duration Phase Count†=1。A=4/0、B=2/0 | Rは夜に出現し、未開始なら次の朝に失効。夜Startなら次の朝にReady | Deadlineは未開始時の期限。開始後のDurationと混同しない。Deadline 0は即EXPIRED | S12.AdvancePhase、S13.StartExtended／Expire |
| 7. Hint条件 | R「HintLevel設定」 | Point Conditions†にLで1件。Source=World、Comparison=Equal、Phase=Day、Add Point†=4。Level 2 Points†=2、Level 3 Points†=4 | 通常の翌朝解決なら4点でLv3 | 出現はNightだがPoint評価は解決時なのでDayを指定する。処理が夜まで遅れればこの条件は不成立。PointだけではHintを付与しない | S13.CalculateHintLevel、S14.ApplyNext |
| 8. 成否・文章 | R基本情報→Start Presentation†／Execution†、Outcome設定の各タブ | Start文章「誰が飲食店を調べますか」。Execution Narrative、Before Judge文章「聞き込みを始める」。Judgeは上表。SUCCESS文章「噂と謝礼を得た」、FAILURE文章「有力な話を聞けなかった」、EXPIRED文章「店は閉まった」。必要ならSpriteを参照 | Start画面、判定前、成功／失敗の文章を設定 | EXPIRED文章は格納できるが現Runnerは表示せず処理する。Outcomeタブの選択自体が成否を固定するのではない | S02.EventPresentation／EventJudgeDefinition、S07.OutcomeEditor、S14.Continue |
| 9. Effect追加 | R Outcome設定→SUCCESS | 「Effects (+ Effect追加)」をLで追加し型GoldEffectを選択、Amount†=100。もう1要素をHintEffect、Is Fixed Hint†=true、Fixed Hint†へ作成したHintをOで参照 | 成功解決時に100Goldと計算済みLevelのHintを適用 | 型選択の実画面未確認。「+ Effect追加」はラベルの一部。FAILURE側は空。必要ならFlagEffect等を追加できるがこの例の必須ではない | S03.GoldEffect／HintEffect、S15.GrantHint、S17 |
| 10. FollowUp | AおよびRのOutcome設定→SUCCESS | Follow Ups†にLで1件。AはDefinition†=R、RはDefinition†=B。両方Delay Phase Count†=1、Allow Concurrent†=false | A成功→次PhaseのR予約、R成功→次PhaseのB予約 | Bも同Catalogへ登録・Enabled=trueにする。旧Start Modeを新方式の時間指定として使わない。予約済みFUはゲームの予約一覧には表示されない | S13.ScheduleFollowUp／ProcessFollowUps |
| 11. 検査 | 各定義のValidator／Database「検査」 | 個別指摘を読み、「全イベント検査」を押す。指摘の「開く」で対象へ移動して修正、再検査 | 設定不備を検出する | 全体検査は他Catalogの定義も対象。関係のない既存データを消して件数0にする手順にしない。対象3定義のID・参照・時間・Slot・Effectsを確認 | S07.Validation、S05.ValidateAll／DrawIssues、S10、S11 |
| 12. 保存 | UnityのProject資産保存操作 | 編集を確定しUnity標準のプロジェクト保存を実行。Catalogと参照アセットも保存。カテゴリ管理を編集した場合は「変更を保存」 | dirtyになったアセットを永続化する | Databaseに全項目共通の独自「保存」ボタンはない。Unity標準メニュー名・ショートカットは今回実画面未確認。Scene保存だけをアセット保存の保証としない | S06.OnInspectorGUI／EditorLifecycle、S05.CreateAsset／RegisterSelected |
| 13. ゲーム確認 | テスト用SceneのEventSystemRoot／ゲーム画面 | テストSceneのRoot.catalogへ上記Catalogを参照。Playして昼のAセルへ移動、現在セルのイベントタブから開きStart→続行。夜まで進めてRへ移動、人物カードを枠へドラッグしてStart。翌朝の実行画面で続行して結果を確定。次の夜にB出現を確認 | A→R→報酬→Bの順に実行するコード経路を通る | 詳細は次節。Sceneコピー・Root設定・Playは今回未実施。マップアイコンを直接クリックして開けるとは確認できない | S21、S22.Select、S23、S25、S26、S28、S14 |

### 3.3 Play確認を具体化する

1. 本番Sceneではなくテスト用コピーで、EventSystemRootのCatalog、UI Prefab、Party／カード関連参照が接続されていることを確認する。Catalogの切替だけで必要なカードや背景アセットまで新規生成されるわけではない。
2. 開始時の昼Phase 0でAの配置を探す。マップの有効な移動1回で時間を1消費する実装であり、期間の初期時間は25。キー割当の実画面は未確認なので、既存ゲームのMove入力を使用する。移動不能セルや入力待ち時間を「1回」と数えない。
3. Aのセルへ移動し、現在セルのイベントタブを選択する。正式な画面上のタブ名は未確認。`HakenTabView`がInstanceのある現在セルから`MapEventController.Select`へ接続する。新イベントでボタン表示は「Start」に切り替わる。
4. Aはカード不要。Start後の実行表示で「続行」を使い、判定前→結果→効果適用／終了まで進める。Aを夜に開始してしまうと、1Phase後は昼となりRのNight条件を満たさず待機するため、例どおりの時間軸にならない。
5. 残り時間を消費して夜Phase 1へ。RのFollowUp処理が出現条件を検査し、空セルがあれば配置する。期間切替ではプレイヤーが拠点へ戻る。RはAの旧セルを優先し、空いていなければ他の空セルへ置く。自動的に予約画面が開くわけではない。
6. 夜が終わる前にRのセルへ行き、人物カードを1枠へ入れてStartする。カード型が一致しても人物のAppealが5以上とは限らない。Start成功時に人物IDを記録してカードを確保し、予約画面が閉じる。
7. 次の昼Phase 2でRがReadyになる。Runnerの優先順によって表示まで待つことがある。実行画面で判定と結果を進め、最初のEffect適用時が昼なら4点・Lv3を計算する。Gold+100、Hint付与、その後B予約の順に進む。
8. 次の夜Phase 3でBが出現する。Bは出現しただけでは結果確定しない。Bへ行きStartし、続行して完了する。
9. 失敗／期限切れはPlayを初期状態からやり直して別途確認する。RでAppeal不足ならFAILURE、RをStartせず朝まで待てばEXPIRED。例では両方とも報酬・B予約なし。

| Phase | この例で期待する状態 | ずれる主な理由 |
|---|---|---|
| 0 昼 | A解決、Rがdue=1で待機 | Aへ到達する前に期間終了、UI未接続、A配置失敗 |
| 1 夜 | R出現、参加者を選びStart、execute=2 | 空セルなし、R無効／未登録、夜のうちにStartできない |
| 2 昼 | R成功でGold+100、Hint Lv3、Bがdue=3 | 判定失敗、先行イベントによる待ち、Continue未操作、Effect失敗 |
| 3 夜 | B出現 | B無効／未登録、同定義が既に活動中、空セルなし |

**UIだけで検証できないこと**：専用のEventHistory閲覧、FollowUp待機キュー閲覧、所有Hint一覧／Levelの編集画面は確認できない。開発者が`EventManager.Progress.history / followUps / hints`、`EventInstance.participantIds / hintLevel`、Partyの所持金状態をデバッガまたはテストで確認する必要がある。一般利用者に「履歴ウィンドウを開く」「予約一覧にBがある」と案内しない。

**報酬表示の注意**：ExecutionView.Showは旧`definition.outcomes`を読んで報酬表示を組み立てるため、新しいtyped Effectsの実適用結果と画面の報酬欄が一致しない場合がある。報酬欄が空だから未付与、表示されたから付与済み、と判断しない。実データの確認が必要。

## 4. 実装状況一覧と検証の限界

次の「実装済み」はソースで処理経路を確認した意味であり、**すべて今回の実機動作未確認**である。「未実装」は今回のプロジェクト横断調査で対応するコード／UIを確認できない意味。

| 分類 | 機能 | 確認内容・限界 |
|---|---|---|
| 実装済み | Database／新規作成／複製／登録／検索／個別Inspector | メニュー、AssetDatabase操作、Odinの編集経路あり。標準Listの見た目は未確認 |
| 実装済み | 拡張7グループと3 Outcomeタブ | FoldoutGroup／TabGroup／GUIColor属性を確認。画面上のレイアウトは未確認 |
| 実装済み | カテゴリ共有・作成・変更・使用停止・旧値移行支援 | 同名正規化と辞書接続。タグとは別の機能 |
| 実装済み | 発生条件のAND、各Source、Latest／Any／All | 空履歴はすべてfalse。発生契機そのものは別途必要 |
| 実装済み | Slot、カード確保、参加人物の記録 | Required／Optional／Involvedの経路、Choiceの枠としての処理あり |
| 実装済み | 期限、開始後待機、昼夜Phase、キャンセル | Duration 0もRunner経由。未開始期限と予約の区別あり |
| 実装済み | Point→Level、固定／ランダムHint、上位Level維持 | 現在の解決状態を評価。Pool単位の抽選履歴 |
| 実装済み | Gold／Item／Hint／Relation／Flagのtyped Effects | 必要な参照・外部状態がそろう場合の適用経路あり |
| 実装済み | FollowUp予約・出現・重複抑制・履歴／保存DTO | 条件不成立や空セル不足では待機。ゲーム全体の保存UIとは別 |
| 実装済み | 個別Validator／全体Validator | 指摘は存在するが保存／Play禁止機構ではない |
| 部分実装 | Choiceを選択肢分岐として使う機能 | enumと任意枠としての処理はあるが、選択肢別Outcome・枝のUIはない |
| 部分実装 | Involvedのゲーム画面表現 | 自動対象選択・ドロップ制限あり。自動選択されたカードの十分な視覚提示は未確認／不足 |
| 部分実装 | 新Outcomeとゲーム内報酬一覧の統合 | 実適用は拡張、報酬リスト表示は旧データ参照 |
| 部分実装 | EXPIREDのPresentation | 入力・保存・検査対象だが現Runnerは表示せず効果処理へ進む |
| 部分実装 | Databaseの逆参照表示 | 従来Outcome等は探索、拡張Outcome／Queryの参照は網羅しない |
| 部分実装 | Flag Scope | Global／Caseは選択できるがCase切替に伴う保存領域分離・自動リセットは確認できない |
| 部分実装 | 汎用Itemカード・楽曲Hintとの連携 | ItemEffectの実体はEquipData。所有Hintから楽曲生成の各種選択肢への対応付けは確認できない |
| 部分実装 | 外部イベント実行／状態連携 | インターフェース等あり。Sceneの外部WorldState／Handlersは未接続、新方式はNarrative前提 |
| 未実装 | 新方式条件の任意OR／括弧・条件グループ | UI・評価木を確認できない。History Anyは条件行のORではない |
| 未実装 | 夜になったらCatalog全体を自動再走査して配置 | AdvancePhaseにその生成経路なし。FollowUp等の契機が必要 |
| 未実装 | 発生場所から飲食店Facilityを自動特定してRelationを評価 | Facility定義と背景／旧施設データは別 |
| 未実装 | Database独自のイベント削除／登録解除／一括保存ボタン | 標準Project／Catalog操作と区別する |
| 未実装 | 拡張7グループ内のタグ・開発メモ・Sort Order編集欄 | データは存在するが専用Modelが公開しない |
| 未実装 | 専用History／所有Hint／FollowUp待機キュー閲覧Editor | 内部データ／テストでの確認が必要 |
| 未実装 | イベント設定からゲーム全体を保存・復元するUI | DTOとイベント状態の復元はあるが、全体の保存画面ではない |

### 4.1 現在のScene・アセットと過去の証跡

- Unity指定版は`ProjectSettings/ProjectVersion.txt`の**6000.3.10f1**。
- `Assets/Scenes/メインバトル.unity`のEventSystemRootは`Assets/イベント/イベントSO/イベントカタログ.asset`を参照している。拡張サンプルCatalogが自動的にゲームで使用される構成ではない。外部worldStateはnull、executionHandlersは空。
- `Assets/EventS/ExtensionSample/SampleCatalog.asset`は`PhaseSample.asset`を登録した別Catalog。PhaseSampleは拡張有効、Flag=false条件、Deadline 2、Duration 1、Night+4点、成功時固定HintとFlag=true、自己FollowUp Delay 1の設定を持つ。本章の飲食店シナリオとは別。
- `Logs/EventExpansion/verified-results.xml`には**66件Passed、0件Failed**の保存結果がある。記録日時は2026-10-02 17:39:47～48 UTC（2026-10-03 02:39:47～48 JST）。当該テストコードには実ボタン／昼夜連携等のテストがあるが、**今回再実行しておらず、現時点の全ファイルとの一致やEditorの見た目・マウス操作を保証しない**。
- 今回Unityは起動していないため、現在の起動可否・コンパイル成功・Odin型選択画面の操作性は未確認。以前のUnity起動失敗の原因解析は本依頼の調査範囲外であり、保存済みテスト成功だけから現在の起動問題が解決済みとは判断しない。

## 5. 不明点・問題点と追加確認事項

### 5.1 操作方法を画面から追加確認すべき箇所

| 項目 | コードから分かったこと | 未確認部分／説明書に必要な追加資料 |
|---|---|---|
| 条件・Slot・Effectの追加削除 | Odin標準List、EffectはSerializeReference派生型 | +／削除／型選択の位置、表示名、Undo時の見え方のスクリーンショット |
| 自動ラベルと色・順序 | 明示属性、Modelの宣言順 | †を付けた名称、Odinの実際のFoldout／Tab描画順・着色範囲 |
| 保存 | 通常編集SetDirty、カテゴリ保存処理、作成時SaveAssets | 実機のUnity標準保存操作と再起動後の永続化 |
| 削除・登録解除 | Database独自ボタンなし、CatalogはList | Project削除時ダイアログ、参照切れ表示、登録Listの実操作。逆参照表示だけでは安全性を判断できない |
| 人物Instance選択 | Scene内PartyのRuntimeを列挙 | 編集モードでRuntime未生成なら候補がAnyだけになり得る。Playで選んだruntimeIdの次回Play有効性 |
| 実行タブ／移動キー | HakenTabとMove入力のコード経路 | 現在のゲーム画面で押すタブ名・位置・キー割当 |
| 報酬・Hint・履歴の目視確認 | 実データはProgress等に保持 | 新Effectを正しく見せる専用プレイヤー画面は確認できない。開発者の状態確認が必要 |

### 5.2 UIから設定しにくい／外部データが必要なもの

- 拡張専用画面ではTags、DeveloperMemo、SortOrder、旧effects等が隠れる。必要なら従来Inspectorのどこで編集可能かをA／F章で確認し、切替の副作用と保存値を確認する。隠れていることと、データが存在しないことは別。
- Sprite、CharaData、EquipData、HintData、FlagDefinitionSO、FacilityDefinitionSO、HintPoolSO、カテゴリ辞書、背景一覧、実行Catalog、SceneのRootとカードUIが別途必要。Definitionだけでは素材や人物を生成しない。
- マップ色`mapColorKey`は固定enumではない。`MiniMapVisualConfig.areaOverlays`の有効な`AreaOverlay.name`との**完全一致**で色を引く。未一致なら共通色。AreaOverlayはname初期`Area`、color初期RGBA(1,0,0,0.5)、enabled初期true。色設定資産の作成メニューは`Assets > Create > Map > VisualConfig`。実際に接続されている設定の名前を使う（S32.ResolveCellColor）。
- Partyの所持金／装備カード、Scene Runtime人物、施設カードと定義IDの対応が必要。旧Facility Listへ行を足すだけでFacilityDefinitionSO条件が設定済みになるわけではない。
- Catalogから外す／無効にする操作は現在進行中Instanceの整合性まで保証しない。保存データ使用中にGUIDやEffect順を変えない。

### 5.3 確認できた実装上の相違・リスク

1. **時刻条件と自動発生の相違**：Nightは真偽判定。昼夜切替時の全定義生成はない。操作例はFollowUpを契機にする。
2. **出現時刻と点数評価時刻の相違**：Pointは解決時。夜出現→翌朝解決のイベントでNight加点を期待すると0点になり得る。
3. **編集可能な結果文章と表示の相違**：EXPIREDの文章は実行時表示されない。
4. **新報酬と報酬欄の相違**：typed Effectsが適用されても旧Outcomeに基づく報酬欄が一致しない。
5. **参照表示と全参照の相違**：Databaseの「参照」で0件でも拡張FollowUp／条件等が参照している可能性がある。
6. **予約という語の二義性**：Start後のReserved Instanceと、未出現のScheduledFollowUp。予約一覧に後者は出ない。
7. **Validator表示と重大度の相違**：DatabaseのInfoがWarningアイコンで表示される。本文と件数を併読する。
8. **検査と実行可否の相違**：エラーでも保存可能。前のEffectを適用してから後のEffectが失敗すると部分適用で停止し、全体が巻き戻るわけではない。
9. **参加者と条件Targetの相違**：Character条件で調べた人物がSlot参加者になる制約はない。Requirementsは参加者の合計条件であり、各Slotに任意の人物条件を置くUIではない。
10. **選択肢に見える型と未実装用途**：Choiceは分岐Outcome指定ではない、ItemEffectはEquipData、Flag ScopeはCase別自動管理を保証しない。
11. **非表示参照の残存**：QueryのSourceを変えても旧参照フィールドは消去されず、非表示eventDefinitionが登録検査に引っかかる場合がある。削除して条件行を作り直すときは、必要な設定を控えてから行う。
12. **全Project検査と選択Catalogの差**：複数Catalog環境では他用途の定義も検査される。選択Catalog、対象定義、辞書の対応を見て判断する。

以上はコード根拠のある指摘。画面に必ずどのような例外・壊れ方が現れるかは実機未確認であり、再現済みバグとして扱わない。Odinの描画、Unity標準操作、個別Sceneの構成で変わる見た目については推測を確定仕様に昇格させない。

## 6. ChatGPT向け解説ガイド

### 6.1 最初に説明する概念

最初に「イベントの設計図を編集し、それをゲームで使うCatalogへ登録する」と説明する。続いて「マップに出現」「人物を選びStart」「時間待ち」「成否判定」「報酬適用」「次のイベントを予約」の順を示す。設定欄を上から読み上げるだけでは、条件・時間・Pointの評価タイミングが伝わらない。

**拡張ライフサイクルを有効にした7グループの操作を主説明にし、従来方式は補足に分離する。** 新規初期値がfalseであることと、有効化前後で見た目が変わることを最初の画面説明に入れる。カテゴリヘッダーは7グループ外の共通UIである。

### 6.2 初心者用語集

| 用語 | 説明するときの意味 |
|---|---|
| ScriptableObject／SO | Unity Projectに保存する設定アセット。ゲーム画面そのものではない |
| EventDefinition | イベントの設計図。1つから複数回発生することがある |
| Event Catalog | このゲームで参照・実行する設計図の登録一覧 |
| GUID／PersistentId | 参照を維持する内部ID。表示名やCodeとは別、通常手入力しない |
| Event Code | 作者が区別する一意の文字列。例DINING_NIGHT |
| Category／Tag | 共有辞書から選ぶ分類／自由文字列の補助ラベル。背景や発生条件ではない |
| EventInstance | 実際に一度出現したイベント。場所、状態、参加者を持つ |
| Phase | 昼または夜の1区間。1Phase経過は昼→夜または夜→翌昼 |
| Condition／Query | 値を調べて成立／不成立を決める式。発生を起動するスイッチとは別 |
| Source／Target／Property／Comparison／Value | 何の種類から、どの対象の、何を取り出し、どう比較し、どの値と比べるか。汎用Target欄が常にあるわけではない |
| Slot／Participant | カードを入れる枠／Start時に参加者として記録された人物 |
| Deadline／Duration／Delay | 未開始の期限／Startから解決可能になるまで／親解決から後続出現まで |
| PointCondition／HintLevel | 解決時に加点する条件／合計点から決まる1～3の情報強度。付与にはHintEffectが別に必要 |
| Outcome／Effect | 成功・失敗・期限切れという結果／結果に伴う所持金等の変更 |
| FollowUp | 結果から別イベントの出現を予約する設定。自動的なStartではない |
| History／Latest／Any／All | 過去結果／最後の1件／いずれか1件／全件。0件ならすべて不成立 |
| Validator | 設定の不備を指摘する検査。正常動作の証明や保存禁止ではない |
| 実機動作未確認 | コードは確認したが、Unity上で今回操作・動作を実証していない |

### 6.3 推奨する説明順序

1. CatalogとDefinition、従来／拡張の違いを説明する。
2. メニューからDatabaseを開き、Catalogを選び、新規作成・拡張有効化を行う。
3. カテゴリ、名前、Code、アイコン・文章を設定し、保存の位置付けを説明する。
4. 「いつ配置を試すか」を説明してからConditionsを設定する。夜の例は3章の導入FollowUpも含める。
5. Slotで人物を決め、Judgeで何を成功条件とするかを説明する。
6. Deadline、Duration、FollowUp Delayを同じ時間軸の図で説明する。
7. Pointを解決時に評価すること、Level、HintEffectを一組で説明する。
8. 3 Outcomeの文章とEffects、FollowUpsを設定する。
9. Validatorの指摘を修正し、アセットを保存する。
10. テストSceneのCatalog接続、Start／続行／時間経過を確認する。画面だけで確認できない報酬・履歴は開発者確認へ分ける。

### 6.4 必ず一緒に説明する組合せ・混乱しやすい箇所

- **Conditions＋配置契機＋Generate On Map**：Night条件だけで夜に出現するとは説明しない。
- **Slot Type＋Accepted Type＋Amount＋Judge＋Requirements**：必須枠と能力判定、旧Required boolを混同しない。
- **Deadline 0／Duration 0／Delay 0**：すべて「即時実行」とひとまとめにしない。
- **Point Conditions＋Level閾値＋HintEffect＋Case Hint Pool**：固定HintにはPool不要、ランダムHintにはPool必須。
- **History対象定義＋History Mode＋履歴0件**：Any／Allを条件行のOR／ANDとして教えない。
- **FollowUp＋対象Catalog登録＋Enabled＋対象Conditions＋空セル＋Allow Concurrent**：予約成功と実際の出現は別。
- **Outcomeタブ＋判定方式＋ゲーム内報酬表示**：タブは編集対象の切替で、必ずその結果になる設定ではない。表示不一致の制限も伝える。
- **ID／Code／表示名／カテゴリ名／背景名**：用途が異なる。人が読みやすい文字列をすべて同じ識別子として扱わない。
- **Info／Warning／Errorと実行可否**：アイコンの見た目だけで重大度や正常動作を判定しない。

### 6.5 実画面のスクリーンショットを撮るべき箇所

| 撮影箇所 | 説明書で確認するもの |
|---|---|
| Tools > Eventsメニュー、Database全体 | 正確な起動位置、Catalog欄、一覧、新規作成・登録・検査ボタン |
| 拡張切替前後のInspector | checkbox名、7グループ、共通カテゴリヘッダー、折りたたみと色 |
| カテゴリSelector・作成・Manager | 検索、未設定、新規追加、変更を保存、使用停止の見え方 |
| Conditions追加前後、WorldとEventHistoryの2状態 | List追加削除、Source変更で変わる欄、History Mode |
| SlotとInvolved設定、実際のカード枠 | Slot Typeと旧Requiredの違い、自動対象表示、ドロップ可否 |
| 時間設定とHintLevel設定 | 3つの時間のうち何がここにあるか、Pointと閾値 |
| SUCCESSのEffect型選択、Gold／Hint、FollowUps | polymorphic Listの操作、固定／ランダム表示切替、対象参照とDelay |
| 個別Validatorと全体検査 | Error／Warning／Info、指摘から開く、Catalog依存の指摘 |
| Root Inspectorと接続Catalog | 制作したアセットを実際にゲームが使う条件 |
| 現在セルのイベントタブ、Start画面、続行画面、予約一覧 | 画面遷移とカード操作、FollowUp予約が一覧に出ない点 |

`Capture UI Preview`で得られるのはゲーム内Prefabのプレビューであり、上記Editor操作画像の代用品ではない。撮影後に本報告書の†ラベルと標準List操作の未確認箇所を補い、実機未検証の記述を根拠なく削除しない。

### 6.6 ChatGPTへ渡すときの指示例

> この報告書を根拠に、Unity初心者向けのイベント編集手順書を日本語で作成してください。3章の構成と時間軸を維持してください。ソース確認済みと実機未確認を区別し、未実装の機能や未確認のボタンを作って説明しないでください。自動ラベル（†）とList操作は実画面確認が必要な旨を残してください。夜条件だけで自動配置される、PointだけでHintが手に入る、FollowUpが予約一覧に表示される、と説明しないでください。操作場所・入力値・結果・注意を各手順に含めてください。
