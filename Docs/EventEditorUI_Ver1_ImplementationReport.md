# イベント編集UI Ver.1 実装報告書

作業日: 2026-10-11。対象: `G:/Unity/Product/Music_Map_Exp`。commit・pushは実施していない。

## 1. 結果と前提

`Tools > Events > Event Editor Ver.1` にUI Toolkit専用EditorWindowを追加した。8タブ、一覧・検索・カテゴリ絞り込み、新規・複製・確認付き削除、条件とPointConditionの連動入力、Slot履歴参照、Outcome別Effect／複数FollowUp、閲覧関係図、Validator、保存・Undo／Redoを実装した。

新しい判定方式やヒント分類は作っていない。既存Odin画面は維持し、旧方式のイベントを自動変換しない。設定SOの一括書換えは行っていない。作業開始時に多数の未コミット変更・未追跡のイベント拡張が存在しており、それらを保持して追加した。Gitの全diffは今回だけの変更ではない。

依頼記載はUnity 6000.2.6f2だが、ProjectVersionとインストール実体は **6000.3.10f1**。検証は後者で行った。

## 2. 実装前調査

既存の `Docs/EventEditorUI_InvestigationReport.md` を読み、Definitions、EventManager／Expansion、Runner、Progress、Instance、CardRegistry、Reservation、World、MapPlacement、Validator、既存Editorとテストを照合した。実装前設計は `Docs/EventEditorUI_Ver1_Design.md` に保存。

| 分類 | 作業前の状態 |
|---|---|
| 実装済み | ダイス＋能力判定、3 Outcome、解決履歴、Latest／Any／All、期限・開始後待機・FollowUp予約と重複抑止、PointCondition、固定／ランダムHint、Odin編集 |
| 部分実装 | FollowUpの予約中はparentIdあり。生成した子には発生元を保持しない。参加者IDは履歴にあるがSlot対応なし |
| 未実装 | 専用UI Toolkit画面、履歴Slot参照、6種類のHintData分類、拡張Queryの所持金・背景カテゴリ比較 |

重大な仕様不明点は、HintDataの6分類と取得内容・既存楽曲ヒントとの接続、拡張Queryへの所持金／背景比較をどの実行時点・コンテキストで行うか。既存経路のない新しいゲームルールは保留した。

## 3. 成功・失敗の現行仕様

`EventRunner.Continue` でNarrativeの初回続行時に判定する。`execution.judge.parameter` の参加カード合計能力＋`modifier`＋ダイス合計が `target` 以上なら成功。ダイスは `useDice` で無効化可能。個数1〜100、面数2〜10000の検証はダイス無効時にも適用される。

乱数seedはInstance生成時に決まり、結果・合計・成功フラグ・結果コードをInstanceへ保存。拡張方式ではSUCCESS／FAILUREに直接対応し、従来方式ではOutcome条件と既定結果を使う。実行画面で判定前文章→続行→結果文章→Effect適用へ進む。EXPIREDは判定をせず、結果画面の表示を経ずに処理する。

作者は使用能力・補正・ダイス設定・目標値・結果内容を編集可能。実際の人物、能力合計、出目、確定Outcomeは実行時に決まる。新UIのOutcomeタブに既存判定の編集を置いた。外部Shop／Offering／Encounterは従来実行ハンドラーの領域で、拡張方式は既存ValidatorどおりNarrativeのみ。

## 4. EventHistory・時間の現行仕様

`EventProgress.history` の `EventHistoryRecord` が、`definitionId`（EventId）、`instanceId`（発生回）、`resolved`、`outcome`、`participantIds` を保持する。拡張方式はEffect完了後、FollowUp予約前に記録。Resolveにも重複防止付き記録がある。成功／失敗の判定直後ではなく効果処理後に履歴へ入る。履歴はinstanceIdで重複防止。

参加者はStart／予約確定時点で記録される。開始前EXPIREDは空。Latestは対象定義の解決済み履歴の末尾、Anyは1件以上一致、Allは全件一致。**0件はすべてfalse**で依頼仕様と一致する。イベントが複数回発生してもdefinitionIdで絞り、instanceIdで発生回を区別する。

- 出現Phase＋deadlinePhaseCount以下の現在Phaseで、AvailableだけをEXPIREDへ。0は出現直後。
- Start後はdurationPhaseCount経過でReady。期限は開始済みイベントに適用しない。
- FollowUpのdelayPhaseCountは親の解決から出現まで。0は解決後に出現を試みる。
- allowConcurrent=falseは予約中・発生中の同一定義を抑止。実際の出現時にも発生中を再検査する。
- 時間帯Queryは発生可否であり、自動生成トリガーではない。基本情報には独立した「時間帯」フィールドは存在しない。

## 5. 新UIの構造・各タブ

左ペインにProject全体のイベントを表示し、登録先カタログを明示する。未登録表示と登録操作、検索（名前・作者用ID・タグ）、カテゴリ絞り込み、選択強調、左右のサイズ変更を提供。右はスクロール可能な8タブ。下部に全体検査件数と選択アセットの保存状態を表示。

| タブ | 実装 |
|---|---|
| 基本 | 名前、作者用ID、内部ID表示、カテゴリ、タグ、メモ、文章・画像、マップアイコン／色／背景カテゴリ、配置設定、優先度、方式 |
| 条件 | 拡張Queryの対象依存入力、数値／真偽／昼夜／Outcome、登録データ選択、Latest／Any／All。従来配置条件も保持 |
| Slot | 枠追加・削除・並べ替え、既存参加方式・合計能力条件、過去参加者参照 |
| 時間 | duration／deadlineと文章プレビュー。FollowUpの時間と重複設定へ誘導 |
| HintLevel | 独立したPointConditionの追加・削除・並べ替え、獲得点、閾値、変換説明、ランダム候補プール |
| Outcome | 既存の成功判定、3結果切替、型別Effect、従来Effect、複数FollowUp |
| 関係図 | 1関係1行のノード・矢印。青実線がFollowUp＋結果名、橙破線が履歴／参加者参照。ノードから基本タブへ |
| 検証 | 既存EventValidatorを再利用。エラー／警告／情報、対象、原因、該当タブへの誘導 |

条件・Effect等は要約付きFoldout。↑／↓で並べ替える。条件の対象変更時は無関係な参照・比較値をクリアする。型付きEffectは型別の追加ボタンから作る。SerializedObject／SerializedPropertyを使用し、データを別の編集用モデルへ複製しない。通常の入力はアセットへ反映し、保存でディスクへ書き込む。UXMLは使わず、C#で構築したUI Toolkit＋USSで実装。UI Builder用テンプレート化は未実施。

旧UIとの違いは、Odinの大きな項目集合をタブと対象依存の入力へ整理し、既存拡張に隠れていたタグ等も表示した点。旧UIそのものを置換・削除してはいない。

## 6. 新データと互換性

| 追加 | 用途・既定 |
|---|---|
| CardSlotDefinition.historyParticipant | 参照イベント・1始まりSlot番号・履歴選択方式・任意Outcome絞り込み。enabledの既定false |
| ParticipantHistorySource | FollowUpParent／LatestMatching |
| EventSlotAssignment | slotNumber→characterId。人物のruntime IDを使用 |
| EventInstance.slotAssignments | Start時に各人物カードを個別に解決したスナップショット |
| EventHistoryRecord.slotAssignments | 上記のコピー。Participantsは引き続き保存 |
| Instance／History.sourceEventInstanceId | FollowUpの発生元。予約のparentIdから引き継ぐ |
| IEventHistoryCardSource | 任意の追加能力。人物IDから使用可能カードを検索。既存IEventParticipantSourceは破壊しない |

新しい保存出力はv3。読込はv1／v2／v3に対応する。既存データに追加フィールドがなくても空として扱い、旧Participantsの並びから補完しない。既存の開始済みInstanceも勝手に割当を推定しない。古いv1／v2の読込、v3 JSON往復をテストした。v3を古いコードへ逆読込する移行は提供しない。

履歴参照は既定無効なので既存Slotの挙動は変わらない。参照有効時は同一人物必須・別人物代替不可。履歴不在、Slot未記録、人物利用不可、別イベントに割当中は開始できない。参照Slotが解決できない理由は開始画面に表示される。LatestMatchingは開始時に対象を選ぶ。親方式は正確な発生回を使用する。

Slotの番号を過去に遡って修正する移行は行わない。既存保存データを運用中にSlotの並べ替え・削除をすると番号の意味が変わるため、UIと手順書に明記した。Scene・Prefab・既存SOの参照を自動置換していない。

## 7. 実装済み／部分実装／保留

実装済み: 専用画面、8タブ、一覧操作、条件とPointの連動、履歴Slot、発生元追跡、複数Effect／FollowUp、保存・再編集、Undo／Redo、Validator、閲覧関係図。

部分実装:

- 所持金は既存の配置条件ResourceAtLeast／Gold、背景カテゴリは既存のマップ配置制限。これを拡張Queryに統合していない。PointConditionには使えない。
- 関係図は読みやすさを優先した1関係1行。ノードが複数行に重複する。自動レイアウト・ズーム・ドラッグ編集はない。
- Validator原因は既存の英語メッセージを含む。修正タブの誘導は原因文字列から推定し、複合問題では基本タブへ案内する場合がある。
- 旧WorldのFlagキーと外部実行キーは、登録辞書がないため自由文字列を維持。新しいFlag条件・Effectはアセット候補式。
- アセット削除は確認と参照元表示・ごみ箱移動を提供するが、Unity Undoでは復元できない。

未実装・保留:

- Move／Density／Order／Rhythm／MaxEmo／MinEmoの分類・取得内容編集。現行HintDataに対応フィールドとランタイム接続がないため。
- キャラクター好感度Effect。既存RelationEffectは施設関係値であり、人物への別ルールは追加していない。
- 特定instanceIdを作者が直接指定するUI。発生前にIDが決まらず、親方式と最新一致方式で今回の用途を満たすため不要と判断。
- OR／条件ツリーは依頼どおり追加しない。

## 8. 検証結果

検証環境: Windows、Unity 6000.3.10f1、EditMode Test Runner（既存テスト内でPlay Modeへの遷移も実施）。元プロジェクトで起動中のUnityがないことを確認して非表示バッチ起動した。

初回の描画無効テストは76件中75件成功。EditorWindow.Showがグラフィックデバイスなしで失敗したため、描画を有効にしたバッチへ切り替え、76件全成功を確認。さらにUIコールバック操作テストを追加した。

**最終結果: 77件成功、0件失敗（既存66件＋今回11件）。コンパイル成功。** `Logs/EventEditorVer1/final-results.xml` と `Logs/EventEditorVer1/final-unity-tests.log` が証拠。UIコールバックからの条件追加・対象変更・演算子切替・値保持・並べ替え・Slot追加・Gold Effect追加が成功。650×420のウィンドウでレイアウトが生成されることを確認したが、文字切れの目視判定までは実施していない。初期Bindingイベントで継続的な再構築が起こらないことも確認した。

`git diff --check` で既存 `Assets/NewEvent.asset` に作業開始前からの末尾空白を検出した。今回の変更とは無関係なのでアセットを書き換えていない。

| シナリオ | 確認内容と限界 |
|---|---|
| A 新規作成 | 一時アセットの作成・新ID・編集・保存・再Import。実UIハンドラーで条件・Slot・Effectを追加。保存先ダイアログの手動クリックは未検証 |
| B 条件連動 | 対象変更で不要フィールド消去。人物の数値演算子6種→時間帯2種へ切替。所持金・背景の拡張比較は未対応 |
| C HintLevel | PointCondition複数の独立保存、通常条件不変。既存テストで点数・閾値・抽選・付与 |
| D 過去参加者 | Aを複数回実行、Bの発生元が古い回でも正しい人物を要求。LatestのOutcome絞り込み、履歴なし、Slot未記録、利用不可、EXPIRED、代替拒否 |
| E 複数FollowUp | B・C予約、重複予約抑止、発生中抑止、allowConcurrent=true許可 |
| F 保存再編集 | SerializedPropertyのUndo／Redo、AssetDatabase保存と強制再Import、v3セーブJSON復元。物理的な閉じる→開くの手動一連操作は未実施 |
| G 互換性 | 既存イベント全件とテスト用イベントを8タブで開き、開くだけではJSONが変わらないことを確認。v1／v2読込。既存66件の回帰テスト |

手動のマウス／キーボード操作、画面キャプチャによる日本語の文字切れ・色・高DPIの目視、Unity 6000.2.6f2、実ゲームの全Scene、音楽再生全体は未検証。UI追加テストはバッチモードにキーボードフォーカスがないためClickableハンドラーを呼び、実際のフィールド変更イベント・Bindingを検査する。物理入力の代替証明ではない。

## 9. 発見した問題・今後の改善

- 所持金／背景カテゴリはマップ配置の条件で、FollowUp出現時の拡張条件と評価経路が違う。UI統合前に仕様確定が必要。
- Hintの6分類と楽曲生成側の関係を定義する必要がある。
- 現行EXPIREDの文章は画面に表示されない。typed Effectとゲーム内の旧報酬表示に差があり得る。今回UI編集以外の演出処理は変更しない。
- Slot番号参照は定義の並べ替えに弱い。将来はSlot自身の安定ID移行を検討する。
- 大規模なイベント群では検索候補のキャッシュ・関係図の集約・検証の差分更新が改善候補。
- Validatorの日本語化と構造化された問題パスを追加すると、より正確に行単位へ誘導できる。
- セーブ再開中にEffect順序やSlot構成を変更する運用は、既存の結果カーソル・参照番号に影響する。

## 10. 今回の変更ファイル

新規:

- `Assets/Script/Events/Editor/EventEditorWindow.cs` — 専用UI Toolkit画面
- `Assets/Script/Events/Editor/EventEditorWindow.uss` — 余白・折返し等のスタイル
- `Assets/Script/Events/Editor/EventRelationGraph.cs` — 関係図
- `Assets/Script/Events/Editor/Tests/EventEditorVer1Tests.cs` — 回帰・互換性・UIテスト
- `Assets/Script/Events/Runtime/EventHistorySlots.cs` — 参照設定・割当・解決処理
- 上記Assetsファイルに対応する `.meta`
- `Docs/EventEditorUI_Ver1_Design.md` — 実装前調査・互換性設計
- `Docs/EventEditorUI_Ver1_ImplementationReport.md` — 本報告書
- `Docs/EventEditorUI_Ver1_UserGuide.md` — 作成者向け操作手順

既存の作業中コードへ追加・変更:

- `Assets/Script/Events/Definitions/EventDefinitions.cs` — Slotに任意参照設定
- `Assets/Script/Events/Definitions/EventExpansionValidator.cs` — 参照Slot／列挙値検査（作業開始時から未追跡ファイル）
- `Assets/Script/Events/Runtime/EventInstance.cs` — 発生元・Slot割当とコピー
- `Assets/Script/Events/Runtime/EventProgress.cs` — 履歴拡張・保存妥当性検査（作業開始時から未追跡）
- `Assets/Script/Events/Runtime/EventManagerExpansion.cs` — 開始時解決、記録、親情報伝達（作業開始時から未追跡）
- `Assets/Script/Events/Runtime/EventManager.cs` — 発生元付き生成、従来StartのSlot記録、保存v3・旧版読込
- `Assets/Script/Events/Runtime/EventCardRegistry.cs` — 同一人物カード検索
- `Assets/Script/Events/UI/ReservationController.cs` — 履歴未解決理由の表示

検証証拠は `Logs/EventEditorVer1/` に保存（通常Git管理対象外）。既存のユーザー変更を含む全git statusと、この一覧を混同しないこと。
