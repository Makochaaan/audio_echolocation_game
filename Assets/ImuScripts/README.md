# ImuScripts — スマホIMUセンサー入力システム

スマートフォンの加速度センサー・ジャイロスコープを利用して、**旋回検知**と**歩数カウント**をゲーム内で使えるようにするスクリプト群です。

## 概要

| スクリプト                         | 役割                                                                                        |
| ---------------------------------- | ------------------------------------------------------------------------------------------- |
| `InterfaceClient.cs`               | IMUセンサーの値を読み取り、旋回・歩数の情報を他のスクリプトへ提供するメインインターフェース |
| `WalkingCalibrationInputSystem.cs` | ユーザーごとの歩行パターンに合わせて歩数検知の閾値を自動キャリブレーションする              |

## セットアップ手順

### 1. ビルド設定の確認

このスクリプトはスマートフォンのセンサーを使用するため、**ビルドターゲットがモバイル（iOS / Android）** になっている必要があります。

- `File > Build Settings` を開き、プラットフォームが iOS または Android になっていることを確認してください。
- **Android の場合:** `Edit > Project Settings > Player > Other Settings` で **Minimum API Level** を **Android 12.0 (API level 31)** 以上に設定してください。Android 12 以降でないとセンサー関連の API が正しく動作しない場合があります。

### 2. Input System パッケージの確認

Unity の **Input System** パッケージを使用しています。まだ導入していない場合は以下の手順でインストールしてください。

1. `Window > Package Manager` を開く
2. 「Input System」を検索してインストール
3. インストール後、Unity の再起動を求められた場合は再起動する
4. `Edit > Project Settings > Player > Other Settings > Active Input Handling` が **Input System Package (New)** または **Both** になっていることを確認する

### 3. GameObject の作成とスクリプトのアタッチ

1. Hierarchy ウィンドウで **空の GameObject を2つ**作成する
   - 例: `ImuInterface`, `WalkingCalibration` など分かりやすい名前を付ける
2. 1つ目の GameObject に **`InterfaceClient`** をアタッチする
3. 2つ目の GameObject に **`WalkingCalibrationInputSystem`** をアタッチする
4. `WalkingCalibrationInputSystem` の Inspector で **`Interface Client`** フィールドに、手順2でアタッチした `InterfaceClient` の GameObject をドラッグ&ドロップして参照を設定する

### 4. Inspector パラメータの設定

#### InterfaceClient

| パラメータ          | デフォルト値 | 説明                                                                                |
| ------------------- | ------------ | ----------------------------------------------------------------------------------- |
| `MAXLEN`            | 50           | yaw（ヨー角）の分散計算に使うサンプル数。大きいほど安定するが反応が遅くなる         |
| `Turn Threshold`    | 60.0         | 旋回と判定するyaw角度の変化量（度）。小さくすると感度が上がる                       |
| `Reset Threshold`   | 0.5          | yawの分散がこの値以下になったとき基準角度をリセットする。静止状態の判定に使用       |
| `Alpha`             | 0.1          | 歩数検知用の指数移動平均（EMA）の平滑化係数。小さいほどノイズに強いが反応が遅くなる |
| `Walking Thredhold` | 0.04         | 歩数を検知する加速度の閾値。キャリブレーション実行後は自動で上書きされる            |
| `Debug Mode`        | false        | true にすると旋回・歩数のログが Console に出力される。動作確認時に便利              |

#### WalkingCalibrationInputSystem

| パラメータ             | デフォルト値   | 説明                                                                 |
| ---------------------- | -------------- | -------------------------------------------------------------------- |
| `Calibration Duration` | 10.0           | キャリブレーションの計測時間（秒）                                   |
| `Clip Duration`        | 1.0            | 計測の先頭と末尾からカットする時間（秒）。開始・終了時のノイズ除去用 |
| `Alpha`                | 0.1            | キャリブレーション用の EMA 平滑化係数                                |
| `Threshold Scale`      | 0.5            | 閾値 = 平均 + 標準偏差 × この値。大きくすると歩数検知が鈍くなる     |
| `Interface Client`     | なし（要設定） | InterfaceClient への参照。**必ず設定すること**                       |
| `Use X/Y/Z Axis`       | Y=true         | キャリブレーションに使う加速度の軸。スマホの持ち方に合わせて選択     |

## 使い方

### 旋回を検知する

他のスクリプトから `InterfaceClient` の参照を取得し、`GetTurnState()` を呼び出します。

```csharp
// InterfaceClient の参照を Inspector で設定するか、Find で取得
[SerializeField] private InterfaceClient imuInterface;

void Update()
{
    int turnState = imuInterface.GetTurnState();

    switch (turnState)
    {
        case 0:
            Debug.Log("右に曲がった");
            // 右旋回時の処理
            break;
        case 1:
            Debug.Log("左に曲がった");
            // 左旋回時の処理
            break;
        case -1:
            // まだ旋回していない（何もしない）
            break;
    }
}
```

**注意点:**

- `GetTurnState()` は呼び出すたびにキューから1つずつ履歴を取り出します（Dequeue）。一度取得した旋回情報は消費されるため、複数箇所から呼ぶ場合は取得した値を変数に保持して共有してください。
- 旋回の判定は `Turn Threshold` で設定した角度分だけyawが変化したときに発火します。

### 歩数を取得する

```csharp
[SerializeField] private InterfaceClient imuInterface;

private int lastStepCount = 0;

void Update()
{
    int currentStepCount = imuInterface.GetStepCount();

    if (currentStepCount > lastStepCount)
    {
        int newSteps = currentStepCount - lastStepCount;
        Debug.Log($"{newSteps} 歩進んだ（合計: {currentStepCount} 歩）");
        // 歩行時の処理
        lastStepCount = currentStepCount;
    }
}
```

**注意点:**

- `GetStepCount()` は累積歩数を返します。「何歩進んだか」を知りたい場合は前回の値との差分を取ってください。
- キャリブレーション前はデフォルトの閾値が使われます。端末や持ち方によって精度が変わるため、キャリブレーションの実行を推奨します。

### キャリブレーションを実行する

キャリブレーションは `WalkingCalibrationInputSystem.StartCalibration()` を呼び出して開始します。UIボタンの `OnClick` イベントに設定するのが最も簡単です。

```csharp
// UIボタンの OnClick に WalkingCalibrationInputSystem.StartCalibration を設定

// または、スクリプトから呼び出す場合:
[SerializeField] private WalkingCalibrationInputSystem calibration;

public void OnCalibrateButtonPressed()
{
    calibration.StartCalibration();
}

void Update()
{
    // キャリブレーション中かどうかの確認
    if (calibration.IsCalibrating())
    {
        Debug.Log("キャリブレーション中...");
    }

    // キャリブレーション完了の確認
    if (calibration.IsCalibrated)
    {
        Debug.Log($"閾値: {calibration.Threshold}");
    }
}
```

**キャリブレーションの流れ:**

1. `StartCalibration()` を呼ぶ
2. ユーザーにスマホを持ったまま歩いてもらう（デフォルト10秒間）
3. 計測終了後、前後の `Clip Duration` 秒を除外したデータから閾値を自動算出
4. 算出された閾値が `InterfaceClient` に自動で反映される

## トラブルシューティング

| 症状                                                         | 原因と対処法                                                                                                          |
| ------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| センサーの値が全く取れない                                   | ビルドターゲットがモバイルになっているか確認。Unity Editor 上ではセンサーは動作しません。実機ビルドで確認してください |
| 歩数が全く検知されない                                       | `Walking Thredhold` が大きすぎる可能性があります。値を小さくするか、キャリブレーションを実行してください              |
| 歩数が過剰にカウントされる                                   | `Walking Thredhold` が小さすぎます。値を大きくするか、キャリブレーションを実行してください                            |
| 旋回が検知されない                                           | `Turn Threshold` を小さくしてみてください                                                                             |
| 旋回が過敏に反応する                                         | `Turn Threshold` を大きくしてみてください                                                                             |
| キャリブレーション結果が不安定                               | `Calibration Duration` を長くする、`Clip Duration` を長くする、歩行中にスマホをなるべく安定させる                     |
| Console に`LinearAccelerationSensor is not available` と出る | 端末がセンサーに対応していないか、Input System パッケージが正しくインストールされていません                           |

