# イベント編集UI Ver.1 横幅・レイアウト修正

作業日: 2026-10-11。Unity 6000.3.10f1で検証。今回の製品コード変更はEditorの表示処理・USSのみ。ゲームロジック、EventDefinitionSO等の構造、既存イベントアセットを変更していない。commit／pushは実施していない。

## 1. 根本原因

### 上部ツールバー

ObjectFieldのラベルと入力が1つのBaseField内にあり、共通のInspector向けラベル幅（min-width: 130px等）と長いカタログ名の内容幅の影響を受けていた。入力にはflex-growのみが指定され、縮小可能な最小幅・flex-basisを明示していなかった。右のアクションボタンも必要な幅を保護していなかった。また、共通ボタンの高さと余白がToolbarの標準高さより大きかった。

修正前の再現テストでは、長いカタログ名を使った1000px幅のウィンドウで、Toolbar右端995pxに対してInspector右端1177pxとなり、約182pxはみ出した。

### HintLevelを含む右ペイン

階層は `TwoPaneSplitView → 右VisualElement → 共通ScrollView → contentContainer → 各タブ`。HintLevel専用のScrollViewやUXMLはなく、全タブで同じコンテナを使っていた。

ScrollViewが `VerticalAndHorizontal` で横方向に内容を広げられる状態なのに、contentContainerと内側のフィールド・長文・Foldoutへ右ペイン幅の制約がなかった。長い候補名や説明文の内容幅が横幅計算に影響し、折返し指定だけでは収まらなかった。ウィンドウ全体の幅を直接代入するコードはなく、横スクロール可能な内容の幅計算と縮小制約の不足が原因。

関係図には別の原因として、行のmin-width: 650px、ノード180px×2、中央290pxという固定指定があった。

## 2. 修正ファイル

製品コード:

- `Assets/Script/Events/Editor/EventEditorWindow.cs`
- `Assets/Script/Events/Editor/EventEditorWindow.uss`
- `Assets/Script/Events/Editor/EventRelationGraph.cs`

追加した検証・報告:

- `Assets/Script/Events/Editor/Tests/EventEditorLayoutTests.cs` と `.meta`
- `Docs/EventEditorUI_Ver1_LayoutFixReport.md`（本書）

作業開始前から存在する変更・削除済みアセットは保持している。今回変更したファイル一覧とリポジトリ全体のgit statusは異なる。

## 3. C#・USSの設定変更

### 上部

- 独立したLabel「登録先カタログ」＋ラベルなしObjectField＋保存＋Inspectorに分離。
- Toolbarは横並び・折返しなし、height:auto、min-height:32px。
- カタログObjectFieldは `flex-grow:1; flex-shrink:1; flex-basis:0; min-width:0`。
- 内部入力・表示要素もmin-width:0と縮小を許可。長い選択名は入力内で省略表示する。
- 見出しとボタンはflex-shrink:0。ボタンは文字と左右paddingに必要な幅を確保し、height:26px・縦margin:0でToolbar内に収める。
- カタログ変更・Save・Inspectorのコールバックは変更していない。

### 共通編集ペイン

- ScrollViewをVerticalへ変更。
- `event-detail-content` は `width:100%; max-width:100%; min-width:0`。幅の基準は右ペイン内のviewport。
- split、右ペイン、ScrollViewのflex-shrinkとmin-heightを調整し、縮小時にもScrollViewが利用可能な高さ・幅に収まるようにした。
- Foldout、PropertyField、BaseField、入力のmin-widthを0にし、内容の長さで親幅を押し広げないようにした。
- フィールド見出しはflex-basis:42%・flex-shrink:1・折返し。既存max-width:235pxは維持。
- HelpBoxの本文はmin-width:0、flex-shrink:1、white-space:normal。条件要約と詳細内のボタンも折返しを許可。
- 長い候補選択値は入力領域内で省略表示。候補の選択機能は維持。

横スクロールバーだけを隠す修正ではない。実際のcontentContainerと子要素の右端がviewport内に収まることを検査した。

### 関係図

- 固定min-width:650pxと固定行高を除去。
- 広い領域では従来の横並びを維持し、中央の接続線を可変幅にした。
- 行幅650px未満ではノードを縦に並べ、矢印も下向きに変更。
- ノード選択・関係の内容は変更していない。

## 4. 実施した動作確認

描画を有効にした非表示Unityバッチで、レイアウト検査2件と既存Ver.1回帰テスト11件を実行。**13件成功、失敗0件、コンパイル成功。**

| ウィンドウ幅×高さ | 左ペイン幅の設定 | HintLevelのviewport／content幅 |
|---|---|---|
| 1000×760 | 180／240／610 | 796／736／366px（それぞれ一致） |
| 650×420（最小） | 180／240／260 | 446／386／366px（それぞれ一致） |
| 1600×1000 | 180／240／1210 | 1396／1336／366px（それぞれ一致） |
| 1920×1080（画面解像度相当） | 180／240／1530 | 1729／1669／366px（それぞれ一致） |

全8タブを各幅で切り替え、Foldoutを展開して測定。長いイベント名・カタログ名・プール名・条件要約・説明を使用した。

確認済み:

- カタログ欄と保存・Inspectorが重ならず、ツールバー右端に収まる。
- 保存・Inspectorの文字幅がボタン内に収まる。
- HintLevelの条件一覧、追加ボタン、閾値、説明、候補プール、警告、Outcome誘導が右ペイン幅内に収まる。
- 折り返すTextElementの必要な文字高さと実際の表示高さを比較し、縦方向にも切れない。
- HintLevelは内容が多いとき縦スクロールでき、横スクロールの必要量は許容誤差内の0。
- 基本・条件・Slot・時間・Outcome・関係図・検証も同じ境界検査に合格。
- 左ペイン幅を変更した後も右ペインが追従する。
- カタログObjectFieldの変更イベント、実フィールドの編集、保存ボタンの処理、保存後の再Import、Inspectorボタンによる選択が動く。保存テストは一時アセットのみを使用して削除した。
- 既存Ver.1テストで条件／ポイント追加・編集・並べ替え、Slot・Effect追加、タブ間保持、Undo／Redo、保存再読込を再確認。
- レイアウト検査前後で対象定義のJSONは同一。Runtime／Definitionsのソース20ファイルと既存EventDefinitionアセット1件はSHA-256が一致。

## 5. 未確認・制限

- ネイティブの最大化状態: バッチのフローティングEditorWindowへ `maximized=true` を指定しても、取得値はfalseのままだった。**OSの最大化操作そのものは未確認**。代わりに画面解像度相当の1920×1080を検証した。
- 分割幅変更はTwoPaneSplitViewの幅設定を通して行った。マウスで仕切りをドラッグする物理操作は未実施。
- ボタンはClickableハンドラー、入力は実フィールドの変更イベントを使用。手動のマウス／キーボード一連操作、スクリーンショットとの目視照合、高DPIや別フォント環境での見た目は未確認。
- 最小ウィンドウサイズ650×420は維持。これより小さなサイズは対象外。
- 今回は表示修正なのでゲーム全体のPlay Mode回帰を再実行していない。ゲームの処理と保存形式には触れていない。

証拠: `Logs/EventEditorLayout/before-results.xml`、`final-results.xml`、`final-unity.log`、`geometry.txt`、`protected-hashes.csv`。Logsは通常Git管理対象外。
