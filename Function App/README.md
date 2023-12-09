# 構成手順
このリポジトリでは、Microsoft Search の検索結果を返却する Azure Functions のサンプルを提供しています。

## 事前準備
開発を開始する前に、以下のツールと SDK をインストールしておく必要があります。

### .NET 7.0 SDK の準備
1. [.NET 7.0 のインストールページ](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)から、.NET 7.0 SDK をインストールしてください。

### Azure CLI の準備
1. [手順ページ](https://learn.microsoft.com/ja-jp/cli/azure/install-azure-cli)に則り、Azure CLI をインストールしてください。

### Azure Functions Core Tools の準備
1. [Azure Functions Core Tools の GitHub ページ](https://github.com/Azure/azure-functions-core-tools) の Installing より、Azure Functions Core Tools をインストールしてください。

### ユーザー認証用のアプリケーションの登録
Microsoft Search API を使用するために、Azure Active Directory アプリケーションを登録します。
1. [手順ページ](https://learn.microsoft.com/ja-jp/graph/tutorials/dotnet?tabs=aad&tutorial-step=1)に則って、ユーザー認証用アプリケーションを登録してください。
1. 登録したアプリケーションページより、左のナビゲーションバーから「API のアクセス許可」を選択し、以下のアクセス許可を追加してください。必要なアクセス許可は、[Microsoft Search API ページ](https://learn.microsoft.com/ja-jp/graph/api/resources/search-api-overview?view=graph-rest-1.0&preserve-view=true#scope-search-based-on-entity-types) の「エンティティ型にも戸津板検索の範囲設定」よりご確認いただけます。
1. 登録したアプリケーションページより、左のナビゲーションバーから「証明書とシークレット」を選択し、新しいクライアント シークレットの作成を行ってください。この際に作成されたシークレットの値は後ほど使用するので、控えておいてください。(クライアント シークレットの値は一度しか表示されないので注意してください。)


### 関数の準備
以下のコマンドでサンプル コードをローカルにクローンしてください。

```cli
git clone https://github.com/marumaru1019/POC-MS-Search-Function
```


## 実行

### Azure リソースの作成と関数のデプロイ
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

## 動作確認
1. MicrosoftSearch 関数の URL を取得して、以下のようにサンプルのリクエストを送信してください。
    ```
    curl -X POST https://poc-ms-graph.azurewebsites.net/api/MicrosoftSearch?code=hMxZQ5WTyActzj-tz_AUek1YRtdJjIZ_P9XcCeuPNbCVAzFuP4JfMA== \
    -H "Content-Type: application/json" \
    -d '{
        "Requests": [
            {
            "EntityTypes": ["driveItem"],
            "Query": {
                "QueryString": "手順書"
            }
            }
        ]
    }'
    ```

    以下のようなレスポンスが返却されれば成功です。

    ```
    {"value":[{"searchTerms":["手順","書"],"hitsContainers":[{"hits": ...
    ```