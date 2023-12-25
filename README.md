# Ignite Japan 2023 での発表内容
[![Video](https://img.youtube.com/vi/2EjYMA_qSjc/maxresdefault.jpg)](https://www.youtube.com/watch?v=2EjYMA_qSjc)

# アーキテクチャー
今回のアーキテクチャーは以下の通りです。

![00](https://github.com/TK3214-MS/POC-Ignite2023-CallAutomation/assets/89323076/4bf0955d-ef78-4ccd-91a4-12e2e8c01120)

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

1. 事前に作成した Event Grid System topic リソースのイベントサブスクリプションを以下のような設定値で作成します。

    ![01](https://github.com/TK3214-MS/POC-Call-Automation/assets/89323076/5f0545b5-1ca2-4658-9cfb-74266c8e0a5b)

1. Event Grid System topic リソースの追加イベントサブスクリプションを以下のような設定値で作成します。

    ![01-01](https://github.com/TK3214-MS/POC-Ignite2023-CallAutomation/assets/89323076/8095b759-85f9-4d82-bbb9-66b9fbdd674b)

### Azure Function App の構成
#### .NET 7.0 SDK の準備
1. [.NET 7.0 のインストールページ](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)から、.NET 7.0 SDK をインストールしてください。

#### Azure CLI の準備
1. [手順ページ](https://learn.microsoft.com/ja-jp/cli/azure/install-azure-cli)に則り、Azure CLI をインストールしてください。

#### Azure Functions Core Tools の準備
1. [Azure Functions Core Tools の GitHub ページ](https://github.com/Azure/azure-functions-core-tools) の Installing より、Azure Functions Core Tools をインストールしてください。

#### ユーザー認証用のアプリケーションの登録
Microsoft Search API を使用するために、Azure Active Directory アプリケーションを登録します。
1. [手順ページ](https://learn.microsoft.com/ja-jp/graph/tutorials/dotnet?tabs=aad&tutorial-step=1)に則って、ユーザー認証用アプリケーションを登録してください。
1. 登録したアプリケーションページより、左のナビゲーションバーから「API のアクセス許可」を選択し、以下のアクセス許可を追加してください。必要なアクセス許可は、[Microsoft Search API ページ](https://learn.microsoft.com/ja-jp/graph/api/resources/search-api-overview?view=graph-rest-1.0&preserve-view=true#scope-search-based-on-entity-types) の「エンティティ型にも戸津板検索の範囲設定」よりご確認いただけます。
1. 登録したアプリケーションページより、左のナビゲーションバーから「証明書とシークレット」を選択し、新しいクライアント シークレットの作成を行ってください。この際に作成されたシークレットの値は後ほど使用するので、控えておいてください。(クライアント シークレットの値は一度しか表示されないので注意してください。)


#### 関数の準備
以下のコマンドでサンプル コードをローカルにクローンしてください。

```cli
git clone https://github.com/marumaru1019/POC-MS-Search-Function
```

#### Azure リソースの作成と関数のデプロイ
1. サンプル コードをクローンしたディレクトリまで移動してください。
    ```
    cd /path/to/POC-MS-Search-Function
    ```
1. Azure にログインしてください。
    ```
    az login
    ```
1. 任意のリージョンにリソース グループを作成して下さい。リソースグループ名は任意のものを指定してください。
    ```
    az group create --name <リソースグループ名> --location <リージョン>
    ```
1. 3 で作成したリソースグループとリージョン内に、Blob ストレージを作成して下さい。ストレージアカウント名は任意のものを指定してください。
    ```
    az storage account create --name <ストレージアカウント名> --resource-group <リソースグループ名> --location <リージョン> --sku Standard_LRS --allow-blob-public-access false
    ```
1. Azure に関数アプリを作成してください。関数アプリ名は任意のものを指定してください。
    ```
    az functionapp create --resource-group <リソースグループ名> --consumption-plan-location <リージョン> --runtime dotnet-isolated --functions-version 4 --name <関数アプリ名> --storage-account <ストレージアカウント名>
    ```
1. 作成した関数アプリに、ClientId、TenantId、ClientSecret を環境変数として設定してください。クライアント ID、テナント ID、クライアント シークレットは、[ユーザー認証用のアプリケーションの登録](#ユーザー認証用のアプリケーションの登録)で作成したものを使用してください。
    ```
    az functionapp config appsettings set --name <関数アプリ名> --resource-group <リソースグループ名> --settings ClientId=<クライアント ID> TenantId=<テナント ID> ClientSecret=<クライアント シークレット>
    ```
1. Azure に関数をデプロイしてください。
    ```
    func azure functionapp publish <関数アプリ名>
    ```

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
  "FunctionsEndpoint": "Function URL",
  "WeatherApiKey": "OpenWeatherMapのAPIキー"
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
