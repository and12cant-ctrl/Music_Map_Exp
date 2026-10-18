# Event Editor Ver.1 実装前の調査・互換性設計

2026-10-11。既存調査報告書を現在のソースと照合。AGENTS.mdはプロジェクト内・上位に見つからなかった。既存の未コミット変更は保持する。

## 実装済み
- EventRunner.Continue: 参加カードの指定能力値合計＋modifier＋任意ダイスの合計がtarget以上なら成功。拡張ではSUCCESS/FAILUREに直結。seedと結果をInstanceに保存。実行画面のContinueで確定する。
- EventHistoryRecord: definitionId、instanceId、resolved、outcome、participantIds。Start時に参加者確定。Effect終了後・FollowUp予約前に履歴登録。Resolveでも重複防止付き登録。
- Latestは解決履歴の挿入順の最後、Anyは1件以上、Allは全件。ただし0件はfalse。
- 期限はAvailableの間だけ適用。出現Phase＋deadline以下の現在PhaseでEXPIRED。0は出現直後。参加者は空。Start後は期限判定しない。
- FollowUp delayは親の解決から出現まで、durationはStartから実行可能まで。allowConcurrent=falseは予約中と発生中の両方を抑止。
- PointConditionは通常条件とは別List。解決時に加点し閾値で1〜3へ変換。Hintは固定／Pool抽選、未出現優先・一巡リセット・高いレベルへの更新のみ。

## 部分実装・未実装
- FollowUp予約にはparentIdがあるが、生成後の子Instanceへ残らない。履歴にSlot対応がない。
- 拡張Queryに所持金／背景カテゴリはない。所持金は旧appearance.conditionsのResourceAtLeast（接続WorldのGold）、背景はappearance.backgroundCategories。新演算子や評価時点は創作しない。
- HintDataはID・表示名・説明のみ。Move/Density/Order/Rhythm/MaxEmo/MinEmoのtyped Hint分類・取得内容との接続仕様は未実装。独自ゲームルールを追加せず保留。
- RelationEffectは施設の関係値。キャラクター好感度ではない。
- ProjectVersionは依頼の6000.2.6f2ではなく6000.3.10f1。実環境版で検証し、指定版での検証とは区別する。

## 変更設計と互換性
専用UI Toolkit EditorWindowを追加。SerializedObject/Property経由の編集、8タブ、登録Catalogを明示。従来方式は自動変換せず従来項目を編集する。Odinを維持。

Slotに任意の過去参加者参照を追加。既定無効。FollowUp発生元／最新の条件一致履歴、参照イベント・1始まりSlot番号、任意Outcomeフィルター、同一人物必須（代替なし）を設定。既存Participantsの順序は一切利用しない。

EventInstanceと履歴にSlotAssignment（1始まりSlot番号→runtime character ID）を追加。Start時に各カードを個別解決してスナップショット保存。発生元InstanceIdも追加。セーブ出力をv3へ、v1/v2読込を維持。古い割当は空／未解決として扱う。既存アセットの一括保存・自動移行は行わない。Slot並べ替えは番号の意味が変わるためUIに注意を表示する。

既存判定、Hint抽選、昼夜境界、Effect実行順は変更しない。未解決理由は予約画面とValidatorで提示する。履歴カード検索は任意追加インターフェースにし既存カード供給実装を破壊しない。
