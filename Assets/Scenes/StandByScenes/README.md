# StandByScenes — ヘッドフォン装着からキャリブレーションまで

StandByScenes 配下は、ヘッドフォン装着後の待機、静止判定、歩行キャリブレーションを順番に進めるためのシーン群です。

## シーン構成

| シーン | 主な役割 | 関連スクリプト |
| --- | --- | --- |
| `1. WaitingScene` | 待機開始用のシーン。ユーザー入力を待ち、装着時の振動を検知する | `DetectStandBy.cs` |
| `2. AdjustingScene` | ヘッドフォン装着後の静止を検知し、案内音声を再生する | `DetectAdjusting.cs` |
| `3. CalibrationScene` | キャリブレーション音声を流し、歩数閾値の調整を開始する | `CalibrationController.cs` |

## 各シーンの動作

### 1. WaitingScene

- 最初に入る待機シーン。
- `DetectStandBy` が IMU の線形加速度を監視し、ヘッドフォン装着時の振動を検知します。
- 振動を検知すると、`2. AdjustingScene` に遷移します。

### 2. AdjustingScene

- `DetectAdjusting` が動作するシーン。
- ヘッドフォン装着後の **5秒間の静止** を検知します。
- 静止が続くと、案内用の音声を流しながら `3. CalibrationScene` へ遷移します。
- このシーンの目的は、キャリブレーション前にユーザーの姿勢を落ち着かせることです。

### 3. CalibrationScene

- `CalibrationController` が動作するシーンです。
- シーン起動と同時に音声を再生し、`calibrationDelayTime` 秒後に `WalkingCalibrationInputSystem.StartCalibration()` を呼び出します。この遅延は、ナレーションとの整合性を取るものです。
- `WalkingCalibrationInputSystem.IsCalibrated` が true になると、完了音声を再生して `TutorialScene1` へ遷移します。
- ここで算出した閾値は、以降の歩数検知に使われます。

## 実行フロー

1. `WaitingScene` で振動を検知する
2. `AdjustingScene` に移動し、5秒間の静止を検知する
3. `CalibrationScene` に移動し、音声再生後にキャリブレーションを開始する
4. キャリブレーション完了後、完了音声を再生して `TutorialScene1` へ遷移する

## 確認してほしい項目

| 項目 | どこで設定するか | 役割 |
| --- | --- | --- |
| `stillnessThreshold` | `DetectAdjusting` | 静止とみなす加速度の閾値。大きいほど厳しく、小さいほど敏感 |
| `stillnessDuration` | `DetectAdjusting` | 静止検知に必要な継続時間。現在は 5 秒想定 |
| `calibrationDelayTime` | `CalibrationController` | `CalibrationScene` で音声を流してからキャリブレーションを始めるまでの待ち時間 |
| `walkingCalibration` | `CalibrationController` | `WalkingCalibrationInputSystem` への参照。必ず割り当てる |
| `completionSound` / `narrationSound` | `CalibrationController` | キャリブレーション完了時に再生する音声 |
| 遷移先シーン | `CalibrationController` の内部処理 | 完了後の遷移先は現在 `TutorialScene1` 固定 |

⇒ 全然全部置き換えでもOK

## セッティングの注意

- `WaitingScene` 側では、`DetectStandBy` に `2. AdjustingScene` へ遷移するための参照設定を行ってください。
- `AdjustingScene` 側では、`DetectAdjusting` に `CalibrationController` の参照を設定してください。
- `CalibrationScene` 側では、`CalibrationController` に `WalkingCalibrationInputSystem` の参照を設定してください。
- 音声再生用の `AudioSource` は、各シーン内のオブジェクトに正しく割り当てる必要があります。
- Unity Editor 上ではセンサー入力が取れない場合があるため、最終確認は実機ビルドで行ってください。
- 各シーンで流れるナレーションはAF2まで配置していますが、それ以降はまだ配置していません。もし作業中に追加されたら、適宜追加してください。
