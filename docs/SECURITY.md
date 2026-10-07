# セキュリティの責務

SuisharpはUIライブラリです。Webアプリ一般の境界防御やユーザー管理を独自に持たず、ASP.NET Coreと配置先の標準機能を利用します。以下はこのPoCの実装範囲と運用側の設定を分けたものです。企業サイトとして安全であることを保証する監査報告ではありません。

## 1. Suisharp固有の対策

- WebSocket受信はテキストフレームに限定し、1メッセージをUTF-8で65,536バイトまでに制限します。超過はWebSocket close code 1009、バイナリは1003で終了します。
- JSONを型付きで解析し、壊れた形式や未定義フィールドは拒否します。未対応イベントは1008、壊れたJSONや形式は1007で終了します。イベントは`click`と`input`だけです。イベント種別とComponentの組み合わせが不正な場合も拒否します。
- 古いブラウザ表示から届いた、既に削除されたComponent IDのイベントは無視します。非表示のComponentやその子へのイベントも実行しません。
- ブラウザ出力ではテキストをDOMの`textContent`またはフォーム値として設定し、HTML文字列として解釈しません。クライアントCSSの値もHTML属性文字列として連結しません。
- WebRendererは1接続につき1インスタンスです。終了時にRenderer自身が保持するルートComponent参照、描画スナップショット、イベント配送先を消し、ルートの描画コールバックを解除します。再接続時は新しいComponent treeとRendererを作ります。SuisharpはセッションID、再接続復元、永続state、接続後の定期タイマーを持ちません。Componentや外部イベント購読、タイマーなどアプリが作ったリソースの解除はアプリ側で行ってください。
- イベントハンドラの例外はRendererが本文を記録せずにASP.NET Coreホストへ伝えます。ホストの標準ログ設定を使い、アプリ側で必要な情報だけを記録してください。

## 2. ASP.NET Core等の標準機能へ委譲するもの

Suisharpは以下の機能を実装しません。アプリの通常のASP.NET CoreパイプラインやWebサーバー、リバースプロキシで設定してください。

- HTTPS/WSS、HTTPからHTTPSへの転送、HSTS
- `AllowedHosts`によるHostフィルタリング
- 信頼するプロキシを限定したForwarded Headers
- Authentication / Authorization
- Rate Limiting、同時接続数や接続元ごとの制限
- Logging provider、ログレベル、保持期間、出力先
- WebSocketのOrigin許可リスト。デモも独自の文字列比較ではなく、`WebSocketOptions.AllowedOrigins`を使います。

デモのページrouteは`Suisharp.Routes.MapGet`からHTTP/1.1 WebSocket用のGETとHTTP/2 WebSocket用のCONNECTを受け付けるASP.NET Core endpointへ登録します。アプリではendpoint routingの`RequireAuthorization()`や`RequireRateLimiting()`を通常どおり適用できます。WebSocket接続の処理が完了するまでendpointは実行中なので、標準の同時実行制限も接続中の枠として使えます。認証が必要か、どのRate Limiting policyを使うかはアプリの要件で決めます。

デモの`appsettings.json`にはローカル起動用の`AllowedHosts`を置き、`Program.cs`では`WebSocketOptions.AllowedOrigins`にローカルの2つのOriginを指定しています。別のホストやポートで動かす場合は、アプリの標準設定から許可するHostとOriginだけを指定してください。Originはスキーム・ホスト・ポートを含めて登録します。

WebSocketのOrigin許可はブラウザからのクロスサイト接続を制限するためのもので、Authenticationの代わりにはなりません。非ブラウザのクライアントはOriginヘッダーを任意に送れるため、アクセス制御が必要な画面では通常の認証・認可も適用してください。CORS設定だけではWebSocketのOriginを制限しません。

## 3. アプリ利用者・運用側の責務

- 本番ではTLSをKestrelまたはTLS終端プロキシで有効にし、ブラウザにはHTTPS/WSSで接続させます。プロキシ配下でForwarded Headersを有効にするときは、信頼するプロキシまたはネットワークだけを登録します。
- 公開するHostを限定し、必要なページとWebSocket endpointにASP.NET Core認証・認可を適用します。Rate Limitingや同時接続数、接続元単位の制限は、アプリの規模・利用者・配置先に応じて標準middlewareやWebサーバー、プロキシで設定します。
- `Style`、`Layout`、`CssClass`に利用者入力をそのまま渡さないでください。値はブラウザのCSSへ適用されます。自由入力を使うならアプリで許可するプロパティ・値を制限してください。
- TextやTextBoxの内容はHTMLとして解釈されませんが、入力値の検証、保存、業務処理、外部出力時のエンコードはアプリ側で行います。
- ログにCookie、認証トークン、入力内容、個人情報などを不用意に記録しないよう、標準Logging providerとアプリの例外処理を設定してください。Suisharpはブラウザから届いた本文をログに出しません。
- CSP、Cookie属性、依存パッケージ更新、バックアップ、監視、障害対応、公開ネットワークの制限も通常のASP.NET Coreアプリの運用責務です。

## デモのタイマーと切断

1秒ごとの更新はデモのサーバー更新例として`SampleApp`が所有する`PeriodicTimer`で動作します。Rendererの通信維持には使いません。WebSocket要求の終了時に`IAsyncDisposable`でキャンセルし、タイマーTaskの終了を待ってからendpointを抜けます。接続が切れたら、その接続用の画面は新しい接続で再生成され、以前の画面は復元しません。

## 今回のAPI・構造上の変更

ページrouteは`Suisharp.Routes.MapGet`からASP.NET Core Endpoint Routingへ登録し、GETとCONNECTを受け付けます。利用者が標準APIでauthorizationやrate limiting metadataを適用できるためです。`MapPost`はMinimal APIへの薄いwrapperです。WebSocket Origin判定は引き続き`WebSocketOptions.AllowedOrigins`へ委譲します。RoutesはWebSocket接続が終わると返されたroot Componentが`IDisposable`または`IAsyncDisposable`の場合にdisposeし、WebRendererも参照を保持しません。これは接続ツリーと接続中リソースを切断後に残さないためのライフサイクルです。詳細は[Web API設計メモ](WEB_API_DESIGN.md)を参照してください。
