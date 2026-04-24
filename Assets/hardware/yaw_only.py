import time
from machine import Pin, I2C
from imu import MPU6050

# ===== I2C & センサ =====
i2c = I2C(0, sda=Pin(0), scl=Pin(1), freq=400000)
mpu = MPU6050(i2c)

# ===== yaw 用のジャイロX軸オフセット補正 =====
# 起動中はセンサを静止させること
def calibrate_gyro_x(samples=500, delay=0.01):
    total = 0.0
    for _ in range(samples):
        gx, _, _ = mpu.gyro.xyz
        total += gx
        time.sleep(delay)
    return total / samples

print("calibrating gyro x... keep sensor still")
gyro_x_offset = calibrate_gyro_x()
print("gyro_x_offset =", gyro_x_offset)

# ===== yaw 初期化 =====
yaw = 0.0
last_ms = time.ticks_ms()

# ===== x軸を yaw とみなして読む =====
def read_yaw():
    global yaw, last_ms

    now_ms = time.ticks_ms()
    dt = time.ticks_diff(now_ms, last_ms) / 1000.0
    last_ms = now_ms

    gx, _, _ = mpu.gyro.xyz
    gx -= gyro_x_offset

    yaw += gx * dt
    return yaw

#  元々の基準となる変数
base_yaw = 0.0
turn_threshold = 50.0
# yaw を時系列で記録する用、分散を計算して閾値以下であればそれはもう90度の角度になってるはず
yaw_list = []
MAX_LEN = 50
reset_threshold = 0.5

def variance(data):
    n = len(data)
    if n == 0:
        return 0

    mean = sum(data) / n
    return sum((x - mean) ** 2 for x in data) / n

# ===== 確認用 =====
while True:
    current_yaw = read_yaw()
    yaw_list.append(current_yaw)
    
    if len(yaw_list) > MAX_LEN:
		yaw_list.pop(0)
    if current_yaw - base_yaw > turn_threshold:
		base_yaw = current_yaw
		print("left turned!")
    if current_yaw - base_yaw < -turn_threshold:
		base_yaw = current_yaw
		print("right turned!")
    if variance(yaw_list) < reset_threshold:
		base_yaw = current_yaw
    # print("d = {:.2f} deg".format(abs(current_yaw - base_yaw)))
    # print("bunsan = {:.2f}".format(variance(yaw_list)))
    time.sleep(0.02)