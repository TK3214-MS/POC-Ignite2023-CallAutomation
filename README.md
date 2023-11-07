# アーキテクチャー
今回のアーキテクチャーは以下の通りです。

![00](https://github.com/TK3214-MS/POC-Call-Automation/assets/89323076/c5e3e1ca-b88c-40b7-8be3-e519ffb9f340)

簡単な流れは以下の通りです。

1. ユーザーが Azure Communication Service に登録された電話番号に架電する。
1. Azure Communication Servicce の Incoming Call をトリガーとしてサーバーサイドアプリケーションの API に Webhook を実行する。
1. Webhook 要求にサーバーサイドアプリケーションが応答する。
1. ユーザー発話内容に基づいて Azure Cognitive Service が Speech-to-Text、Azure OpenAI Service からの応答内容に基づいて Text-to-Speech を実行する。
1. ユーザー発話内容に基づいて Azure OpenAI Service が回答内容を生成する。

# 構成手順
以下を構成、実装する事で動作確認用環境の構成が可能です。

## 事前準備
### Azure Open AI リソースの作成
1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/ai-services/openai/how-to/create-resource?pivots=web-portal)に則り、Azure Open AI Service リソースを作成します。

1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/ai-services/openai/how-to/create-resource?pivots=web-portal#deploy-a-model)に則り、モデルを作成します。

    ※本サンプルでは gpt-35-turbo-16k を利用しました。

### Azure AI multi-service account リソースの作成
1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/ai-services/multi-service-resource?tabs=windows&pivots=azportal)に則り、Azure AI multi-service account リソースを作成します。

1. [Azure ポータル](https://portal.azure.com)より作成した Azure AI multi-service account リソースの [ENDPOINT]値をコピーします。

### Event Grid System topic リソースの作成
1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/communication-services/concepts/call-automation/incoming-call-notification#receiving-an-incoming-call-notification-from-event-grid)に則り、Azure Communication Service 向け Event Grid System topic リソースを作成します。

### Azure Communication Service リソースの作成
1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/communication-services/quickstarts/create-communication-resource?tabs=windows&pivots=platform-azp#create-azure-communication-services-resource)に則り、Azure Communication Service リソースを作成します。

1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/communication-services/quickstarts/create-communication-resource?tabs=windows&pivots=platform-azp#access-your-connection-strings-and-service-endpoints)に則り、接続文字列をコピーします。

1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/communication-services/quickstarts/telephony/get-phone-number?tabs=windows&pivots=platform-azp)に則り、電話番号を取得します。

1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/communication-services/concepts/call-automation/azure-communication-services-azure-cognitive-services-integration)に則り、Azure AI Service へのアクセス許可を Azure Communication Service マネージド ID に与えます。

### Azure DevTunnel の構成
1. [手順ページ](https://learn.microsoft.com/ja-jp/azure/developer/dev-tunnels/get-started?tabs=windows)に則り、DevTunnel CLI をインストールします。

1. 以下コマンドを実行し、DevTunnel をホストします。

    ```powershell
    devtunnel create --allow-anonymous
    devtunnel port create -p 5165
    devtunnel host
    ```

    出力された[Connect via browser:]に続く URL 値をコピーします。

1. 事前に作成した Eventt Grid System topic リソースのイベントサブスクリプションを以下のような設定値で作成します。

    ![01](https://github.com/TK3214-MS/POC-Call-Automation/assets/89323076/5f0545b5-1ca2-4658-9cfb-74266c8e0a5b)

## 実行
### 構成ファイルの定義
[appsettings.json]ファイルを以下の通り環境値に置き換えます。

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DevTunnelUri": "コピーした Azure DevTunnel URL",
  "CognitiveServiceEndpoint": "Azure AI multi-service account の Endpoint",
  "AcsConnectionString": "Azure Communication Service の接続文字列",
  "AzureOpenAIServiceKey": "AOAI KEY",
  "AzureOpenAIServiceEndpoint": "AOAI ENDPOINT",
  "AzureOpenAIDeploymentModelName": "AOAI モデル名",
  "BlobConnectionString": "Azure Storage の接続文字列",
  "FunctionsEndpoint": "Function URL"
}
```

### アプリケーションの実行
1. ルートフォルダーで以下コマンドを実行します。

    ```powershell
    dotnet run
    ```

1. 以下のような出力が表示されるとサーバーサイドアプリケーションの実行完了です。

    ![02](https://github.com/TK3214-MS/POC-Call-Automation/assets/89323076/b51ae720-9b4a-435c-8ac2-acf1ab8cfd7b)

## 動作確認
1. 取得した電話番号に対して架電を行います。

1. 任意の質問を問いかけ、日本語で応答があれば動作確認完了です。