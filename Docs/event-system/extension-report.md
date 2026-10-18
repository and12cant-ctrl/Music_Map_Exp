# イベントシステム拡張・実装報告

## 最初の調査結果

1. **既存機能**：EventDefinitionSO／Catalog、定義GUIDと発生GUID、MapCellへの配置、予約カードID、昼夜をPhaseへ変換するRoot、予約→Ready→Running→Resolved、効果・後続処理の再開カーソル、EventSaveData、OdinのDatabaseとValidatorが存在した。
2. **一致していた設計**：同一定義を複数Instanceとして生成できる。条件はAND。FollowUpはEffectsから独立。人物はCharacterRuntimeDataでruntimeIdを持つ。SlotからIEventCardSourceを通してカードを扱える。
3. **変更箇所**：期限と所要時間の分離、Start時の人物ID確定、型付き条件、結果3種類、遅延予約、履歴、Hint所有／レベル、Inspector。HintSelectionManagerの上限は2だったため3へ変更した。
4. **追加データ**：Flag／Facility／HintPool定義、OccurrenceCondition／PointCondition、5種類のOutcomeEffect、履歴・FlagState・HintOwnership・HintDrawHistory・ScheduledFollowUp等。
5. **既存データへの影響**：useExtendedLifecycleは既定false。既存のoutcomes・conditions・requiredPhaseCount・required等を保持し、旧イベントは従来設定で動く。CardTypeは末尾追加。既存IDとUnityのmeta GUIDを変更していない。新規定義は生成時からIDを持つよう修正した。
6. **移行上の危険**：未設定の0を全既存イベントの即時期限にすること、人物と物理カードのID混同、効果の再適用、マップ再生成による周回状態の消失、ScriptableObjectのIDがメモリにだけ存在すること。これらを実装とテストで対処した。

PartyStatusという独立クラスは見つからず、PartyManager／PartyRuntimeを利用した。CharaDataは人物テンプレート、CharacterRuntimeDataは人物個体、CharacterGeneratorは個体生成を担当する。施設の既存ランタイムはなく、人物とは別に追加した。AnswerSetとHintSelectionStateの既存音楽要素構成は変更していない。ゲーム全体をディスクへ保存する仕組みは見つからず、既存のEventSaveDataを拡張した。

## 1. 変更・追加ファイルと役割

パスはプロジェクトルート `G:/Unity/Product/Music_Map_Exp` からの相対表記。

| ファイル | 内容 |
|---|---|
| Assets/Script/Events/Definitions/EventDefinitionSO.cs | 新方式の切替、拡張設定、実効Outcomeの取得、生成時GUID |
| Assets/Script/Events/Definitions/EventDefinitions.cs | 4種類のSlot、型付きEffects、遅延FollowUpを既存構造へ追加 |
| Assets/Script/Events/Definitions/EventExpansion.cs | Source／Operator等のEnum、連動条件UI、別構造の通常条件とPoint条件、時間・結果設定、5種類のEffect |
| Assets/Script/Events/Definitions/EventIdentitySO.cs | 新しい参照定義の安定ID・表示名・説明の共通基底 |
| Assets/Script/Events/Definitions/FlagDefinitionSO.cs | bool Flagの定義、将来用Scope |
| Assets/Script/Events/Definitions/FacilityDefinitionSO.cs | Characterから独立した施設定義 |
| Assets/Script/Events/Definitions/HintPoolSO.cs | 同一事件で共有するランダムHint候補プール |
| Assets/Script/Scritable/HintData.cs | 空だったHint定義へ安定ID等を追加 |
| Assets/Script/Events/Definitions/EventValidator.cs | 旧検査を維持、新方式への分岐、Info、拡張FollowUpの循環検出 |
| Assets/Script/Events/Definitions/EventExpansionValidator.cs | 新しい条件・Phase・Slot・Hint・Flag・Outcome・FollowUpを検査 |
| Assets/Script/Events/Runtime/EventProgress.cs | 履歴・世界状態・Hint所有・抽選履歴・FollowUp保存データ、Effect実行コンテキスト、人物参照契約 |
| Assets/Script/Events/Runtime/EventManagerExpansion.cs | 条件評価、Start、期限切れ、Point計算、履歴追加、後続予約と出現 |
| Assets/Script/Events/Runtime/EventManager.cs | 既存Managerへの接続、version 2保存、version 1読込、状態検証 |
| Assets/Script/Events/Runtime/EventInstance.cs | 出現／期限Phase、参加人物ID、確定済みHintLevel、拡張保存領域 |
| Assets/Script/Events/Runtime/EventRunner.cs | 3結果選択、EXPIRED処理、共通Effectカーソル、履歴→FollowUpの順序 |
| Assets/Script/Events/Runtime/EventEffectExecutor.cs | 型付きEffectsから既存Gold／Equip処理等への接続 |
| Assets/Script/Events/Runtime/EventCardRegistry.cs | 現在の人物能力参照、安定人物ID、Involved候補、施設カード |
| Assets/Script/Events/Integration/EventCardIdentity.cs | 施設・装備カードの定義参照と復元可能なカード個体ID |
| Assets/Script/Events/Integration/EventMapPlacement.cs | 通常配置候補に新しいConditionsを適用 |
| Assets/Script/Events/Integration/MapEventController.cs | 拡張イベントをStart画面へ接続 |
| Assets/Script/Events/Integration/EventSystemRoot.cs | 昼夜接続を再利用、マップ再生成で周回状態保持、Hint表示の復元 |
| Assets/Script/Events/UI/ReservationView.cs | Duration表示、Start表示、Slot種類とInvolved対象表示 |
| Assets/Script/UI/カード/CardSlot.cs | 自動参加枠への手動投入防止、施設を数量カードとして複製しない処理 |
| Assets/Script/UI/カード/CardDrag.cs | CardTypeにFacility／Itemを末尾追加 |
| Assets/Script/発表用/HintSelectionManager.cs | 楽曲生成用の一時選択上限を3に変更 |
| Assets/Script/Events/Editor/EventCategoryEditorTools.cs | 既存Inspector／Databaseから専用編集表示へ接続 |
| Assets/Script/Events/Editor/EventExpansionEditorModel.cs | 同じ実データを編集する7グループ・色分け・結果タブ |
| Assets/Script/Events/Editor/EventExpansionSampleCreator.cs | 本番Catalogへ混ぜず、検証用1イベントと参照資産を作成 |
| Assets/Script/Events/Editor/Tests/EventExpansionTests.cs | 新仕様の回帰、実UI・昼夜統合、保存・ID再読込テスト |

新規C#とサンプル資産には対応する.metaを含む。サンプルは `Assets/EventS/ExtensionSample/` のPhaseSample、SampleCatalog、SampleCategories、SampleBlocked、SampleCompleted、SampleHint、SampleCasePool。既存本番イベントの大量作成・一括変換は行っていない。

## 2. ランタイムの役割と順序

EventManagerが発生とPhaseを管理し、EventRunnerが従来どおり表示と結果適用を進める。新方式では `extension` の設定が有効になる。

1. Conditions全成立で配置可能。直接Spawnとマップ生成の両方で評価する。
2. 発生時にDeadlineの絶対Phaseを設定。0なら直ちにEXPIREDのReadyになる。
3. Start時にSlotを検証し、Involvedを自動補完。人物のruntimeIdをparticipantIdsへ固定する。
4. Duration 0ならReady、それ以外はReserved。昼夜の切替で期限に到達した予約をReadyへ進める。
5. 既存の判定値・ダイス処理からSUCCESS／FAILUREを選ぶ。EXPIREDは判定せず、表示画面が未接続でも効果処理へ進める。
6. 解決処理開始時にPoint条件を評価し、HintLevelを一度だけ確定。Effectsを順に適用する。
7. 履歴をInstance IDごとに一度だけ追記し、FollowUpを予約。親をResolvedにしてセルを解放した後、遅延0のFollowUpを出現させる。

World CurrentPhaseはDay／Night。内部の経過Phaseは `2 × (日付 - 1) + (Nightなら1)`。deadlinePhaseCountとdurationPhaseCountは経過量であり、Day／NightのEnumとは別。

## 3. 互換性と保存

- 既存方式：reservation.requiredPhaseCount、旧条件、旧outcomesを使用。
- 新方式：extension.deadlinePhaseCount／durationPhaseCount、Conditions、3つの専用Outcomeを使用。Slotリストと判定・表示設定は既存のものを再利用する。
- 新Outcomeも内部ではEventOutcomeDefinition。旧effectsリストは互換用として残り、その後に共通のtypedEffectsリストを適用する。新方式専用画面はtypedEffectsを「Effects」として表示する。
- EventSaveData version 2にはInstance、履歴、Flag、施設Relation、FollowUp、Hint所有／Level、抽選履歴、結果カーソルが入る。JsonUtilityで往復可能。version 1も受け付ける。
- CreateSaveDataは可変Listを分離してコピーする。壊れたprogressを含む保存は現在状態を保持して拒否する。
- ロードでRunningはReadyへ戻し、適用済みEffect数から再開する。人物・時間・Partyの保存は既存契約どおり呼出側が先に復元する。
- 周回中のマップ再生成ではProgressを保持する。新しい周回で初期化する場合は新しいEventSaveData／EventManagerを使う。Global／Case Scopeは定義上の区分のみで、自動的な事件境界リセットは今回導入していない。

## 4. 新イベントの作成手順

1. `Tools > Events > Event Database` でCatalogを選び、「新規イベント」を作る。カテゴリを選択し、Catalogへ登録する。既存の複製コマンドは新しい定義GUIDを発行する。
2. InspectorのExtensionで `Use Extended Lifecycle` を有効にする。以降は専用の7グループ表示になる。
3. 基本情報で表示名・Code・マップアイコン・配置数／重み・開始時文章・実行時文章・判定設定を設定する。
4. Slot設定で枠とCardTypeを選ぶ。人物はChara、施設はFacility、既存装備はEquip。施設をCharacterへ変換する必要はない。
5. 時間設定でDeadlineとDurationを指定する。Deadline 0はStartできず期限切れ。Duration 0はStart直後に解決待ちになる。
6. Conditions、HintLevel、3つのOutcomeを設定し、ValidatorとDatabaseの全イベント検査を実行する。

Requiredは未投入ならStart不可。Optional／Choiceは空でもStart可能。Choiceの選択はassignedCardIdsのSlot位置に保持される。Involvedは対象のCharaData／FacilityDefinitionSO／EquipDataを選び、自動参加対象として表示する。プレイヤーが交換・取り外しできる枠にはしない。対象カードが存在しない／他イベントで使用中ならStartを拒否する。

施設・装備のUIカードには必要に応じてEventCardIdentityを付け、definitionを設定する。人物は既存のカード生成とruntimeIdを利用する。外部のカード保存処理はEventCardIdentity.InstanceIdを保存し、生成後RestoreIdentityで戻せる。

## 5. Conditionの設定

発生条件のConditionsに要素を追加し、QueryのSourceを選ぶ。Targetと値の欄はSourceに連動する。複数要素は全てAND。

| Source | Target | Propertyと値 |
|---|---|---|
| Character | CharaData、必要なら再生中の個体選択 | EventParameterと整数。Charmに相当する既存名称はAppeal |
| Facility | FacilityDefinitionSO | Relationと整数 |
| Flag | FlagDefinitionSO | bool Value |
| Hint | HintData | Ownedとbool |
| World | 不要 | CurrentPhaseとDay／Night |
| EventHistory | EventDefinitionSO、Latest／Any／All | OutcomeとSUCCESS／FAILURE／EXPIRED |
| EventParticipant | EventDefinitionSO、Latest／Any／All | その履歴の参加人物の現在能力と整数 |

整数には6種類の比較、bool・Phase・OutcomeにはEqual／NotEqualが候補になる。Characterの個体を未指定なら、そのCharaDataから生成された現在のパーティー人物のうち1人以上が成立すればtrue。個体指定は自由入力ではなく再生中の候補から選ぶ。

履歴が0件ならLatest／Any／Allはいずれもfalse。EventParticipantでは、まず各履歴内に条件を満たす人物が1人以上いるかを評価し、その真偽へLatest／Any／Allを適用する。参加人物が空／現在の人物一覧に存在しない場合は成立しない。過去の能力Snapshotは保存しない。

## 6. PointConditionの設定

HintLevel設定のPointConditionsへ追加する。通常条件とは別クラス・別リストだが、同じQuery UIを利用できる。成立した要素のaddPointだけを独立に加算する。ANDで全体を合否判定するリストではない。

既定の閾値は2点以上Lv2、4点以上Lv3、それ未満Lv1。Level2Points／Level3Pointsで調整する。評価時点は解決処理開始時で、結果EffectによるFlag変更等の前。保存・再開では確定値を再利用する。

## 7. OutcomeとEffectの設定

Outcome設定のSUCCESS／FAILURE／EXPIREDタブで、PresentationとEffects、FollowUpsを編集する。Effectsの追加から具体型を選ぶ。

- GoldEffect：amountを加算。負数は支払いとして既存の所持金検査を通す。
- ItemEffect：既存のEquipDataをPartyのEquipsへ付与。初期実装のItem報酬はこの既存在庫を利用する。
- HintEffect：isFixedHint=trueならfixedHintを指定。falseならイベントのCaseHintPoolから抽選する。
- RelationEffect：指定FacilityのRelationをamountだけ変更する。
- FlagEffect：指定Flagのboolを変更する。

新しいEffectはSerializableなOutcomeEffect派生型とApplyを追加する。Outcome側に種類別リストを増やさない。UnityのSerializeReferenceで型を保存する。

ランダムHintはPoolの安定ID単位で抽選履歴を共有し、一巡するまで重複させない。一巡後に消すのは抽選済み履歴だけ。所有Hintは残り、同じHintはLevelが高い場合だけ更新する。所有の正本はProgress.hints。既存PartyRuntime.Hintsへも表示用参照を反映する。HintSelectionStateはこれとは独立した、今回の楽曲生成の最大3項目の一時選択である。

## 8. FollowUpの設定

FollowUpsにEventDefinitionを指定し、delayPhaseCountとallowConcurrentを設定する。拡張方式では旧startModeは使わない。後続はAvailableとして出現し、カードを自動的に奪ってStartする処理にはしない。

- delay 0：親のセル解放後に同じPhase内で出現。
- delay 1以上：現在Phaseに加算して保存。到達したPhaseで出現処理。
- allowConcurrent=false：別のAvailable／Reserved／Ready／Running、または保留中FollowUpが同じ定義なら抑止する。解決中の親自身は除外するので、同一定義を繰り返す後続を設定できる。
- allowConcurrent=true：個別のInstanceとして追加できる。

出現条件が未成立、定義が無効、空きセルがない場合は予約を保留し、次のPhase等で再試行する。予約後に別の同一定義が出現していた場合、非Concurrent予約は重複出現を抑止して終了する。即時期限と即時FollowUpが連鎖する危険な循環にはValidatorとランタイム保護を設けた。

## 9. Flagの作成・利用

`Create > Events > Flag` でFlagDefinitionSOを作り、表示名・説明・Scopeを設定する。FlagEffectとConditionの両方で同じ資産を参照する。未設定のランタイム値はfalse。定義資産自体にプレイ中のboolを書き込まない。

Flag／Facility／Hint／Poolを別概念として新規作成するときはCreateメニューを使う。既存資産の単純コピーはIDもコピーするため、別定義の作成方法としては使用しない。IDの自動再採番で既存セーブの参照を壊す処理は入れていない。

## 10. テストイベントと確認結果

`Assets/EventS/ExtensionSample/PhaseSample.asset` が検証用の1イベント。専用SampleCatalogを使用し、本番Catalogへは自動登録しない。`Tools > Events > Create Extension Sample` は未作成時だけ生成する。

条件はSampleBlocked=false。人物Required枠1つ、Deadline 2、Duration 1。Nightで解決すると4点でHint Lv3。SUCCESSでSampleHint付与とSampleCompleted=true、1 Phase後に同じ定義を再出現させる。成功判定は既定でダイスなし・目標0。

検証結果と実行ログは `Logs/EventExpansion/` に保存。実UIのテストは一時シーン上で既存EventSystemUI.prefab、EventSystemRoot、PartyManager、CharacterRuntimeData、CardDrag／CardSlot、実際のconfirmButton、TimeManager.EndPeriodを使用する。ドメイン再読み込みを跨いでPlay Modeへ入り、以下を確認した。

- 条件判定→マップ配置→カード投入→Startボタン→人物ID固定→Night→SUCCESS→Hint／Flag→履歴→FollowUp予約→翌Dayで別Instance。
- Deadline 0の出現直後期限切れ、EXPIREDの参加者空、Duration 0、FAILURE。
- 同一定義の繰返し、Latest／Any／All、履歴0件、過去参加者の現在能力参照。
- FollowUpの遅延0、Concurrentの有無、出現中・将来予約中の重複抑止。
- 固定／ランダムHint、抽選の一巡、低Levelで更新しないこと、高Levelへの更新。
- 5種類のEffect混在、施設と人物の分離、Optional／Choice、Involvedの自動参加・交換拒否。
- JSON保存復元、実行途中で保存したEffectの二重適用防止、無効な保存の拒否、Hint表示の復元、マップ再生成での周回状態保持。
- サンプルのIDがファイルに保存され、再Import後も変わらないこと。

最終結果は **66件成功、0件失敗**（既存45件＋追加21件）。`verified-results.xml` と `verified-unity-tests.log` を参照。Unity 6000.3.10f1でコンパイルと全テストを実行した。nographicsでの実行なので、描画の目視、マウスを使うドラッグ操作、Inspectorの見栄え、音楽再生は今回の自動検証範囲に含まない。

## 11. 未実装・対象外

- 音楽の5要素目、A/B/Cテンプレート生成、具体的な本番イベント、大量データ、最終バランス、美術調整。
- ゲーム全体のディスク保存UIとParty／Time／Character復元。今回はイベント状態のDTOと既存復元入口まで。
- Case切替時のScope別初期化、人物がパーティー外へ移動した後の世界全体の人物検索、施設カードの新しいパレット画面。
- ItemEffectは既存EquipDataを利用。装備以外の独立した消費アイテム在庫は導入していない。
- Choice専用の分岐記述言語は追加せず、既存のSlot順カードIDと判定への参加として保持する。

## 12. 技術的負債と運用注意

- 実行途中のセーブが参照する定義のEffect順序や型を編集すると、カーソルの意味が変わる。件数等の不整合は検出するが、定義の完全な履歴バージョン管理は未導入。
- 型名をSerializeReferenceに保存するため、Effect派生クラスの改名・namespace変更には移行対応が必要。
- 定義IDの重複コピーを避けること。Event Databaseのイベント複製は再採番するが、Unity標準の資産コピーは別扱い。
- Characterの個体IDは既存CharacterRuntimeData.runtimeId。全体ロード側が新規生成せず同じIDを復元する必要がある。人物カードは既存UIに再生成されていることが判定再開の前提。
- 古いFlagの自由文字列APIは互換性のため残している。新しいFlagDefinitionSOの状態とは自動で同一視しない。
- 拡張イベントはNarrative方式を対象にする。既存Shop／Offering／Encounterは従来方式を維持。
- 履歴とResolved Instanceは周回内で蓄積する。長期運用ではセーブ容量と履歴検索索引の検討が必要。

## 13. Inspector／Databaseの使い方

拡張方式では、基本情報（灰）、発生条件（青）、Slot（緑）、時間（黄）、HintLevel（紫）、Outcome（赤）、Validator（橙）へ整理した。通常InspectorとEvent Database内の選択イベント表示で同じ編集モデルを使用する。これは別データへのコピーではなく、元EventDefinitionSOのフィールドを編集する画面。

Sourceと参照先はEnum／Object選択を優先する。通常の数値や文章、表示名、既存のeventCodeなど意味のある入力欄は残す。Validatorでは空画像・空文章はWarning、Deadline 0はInfo、欠落した参照・負のPhase・不足Outcome等はErrorとして示す。Databaseの検査ではカテゴリ・Catalog登録・ID重複・循環も確認する。

旧方式へ戻す切替は既存設定を表示するために残している。運用中のイベントを勝手に変換するものではない。新方式を本番へ採用するときは、まずサンプルCatalogで操作を確認し、対象イベントを明示的に登録する。
