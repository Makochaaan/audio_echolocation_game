import time
import network
import socket
from machine import Pin, I2C
from imu import MPU6050

# =========================
# Wi-Fi 設定
# =========================
SSID =  "Buffalo-2G-9D20"
PASSWORD = "srwntvp846r8r"

# =========================
# I2C & センサ
# =========================
i2c = I2C(0, sda=Pin(0), scl=Pin(1), freq=400000)
mpu = MPU6050(i2c)

# =========================
# ジャイロX軸オフセット補正
# =========================
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

# =========================
# yaw 初期化
# =========================
yaw = 0.0
last_ms = time.ticks_ms()

def read_yaw():
    global yaw, last_ms

    now_ms = time.ticks_ms()
    dt = time.ticks_diff(now_ms, last_ms) / 1000.0
    last_ms = now_ms

    gx, _, _ = mpu.gyro.xyz
    gx -= gyro_x_offset
    yaw += gx * dt
    return yaw

# =========================
# turn 判定用
# =========================
base_yaw = 0.0
turn_threshold = 50.0
yaw_list = []
MAX_LEN = 50
reset_threshold = 0.5

latest_turn = "none"

def variance(data):
    n = len(data)
    if n == 0:
        return 0.0
    mean = sum(data) / n
    return sum((x - mean) ** 2 for x in data) / n

def update_turn_state():
    global base_yaw, latest_turn

    current_yaw = read_yaw()
    yaw_list.append(current_yaw)

    if len(yaw_list) > MAX_LEN:
        yaw_list.pop(0)

    diff = current_yaw - base_yaw

    if diff > turn_threshold:
        base_yaw = current_yaw
        latest_turn = "left"
        print("left turned!")

    elif diff < -turn_threshold:
        base_yaw = current_yaw
        latest_turn = "right"
        print("right turned!")

    if variance(yaw_list) < reset_threshold:
        base_yaw = current_yaw
        

# =========================
# Wi-Fi 接続
# =========================
def connect_wifi():
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)

    if not wlan.isconnected():
        print("connecting to Wi-Fi...")
        wlan.connect(SSID, PASSWORD)

        timeout = 15
        start = time.time()
        while not wlan.isconnected():
            if time.time() - start > timeout:
                raise RuntimeError("Wi-Fi connection timeout")
            time.sleep(0.5)

    ip = wlan.ifconfig()[0]
    print("Wi-Fi connected")
    print("IP address:", ip)
    return ip

# =========================
# HTTP レスポンス
# =========================
def send_turn_response(client, turn_text):
    body = turn_text.encode("utf-8")
    header = (
        "HTTP/1.1 200 OK\r\n"
        "Content-Type: text/plain; charset=utf-8\r\n"
        "Access-Control-Allow-Origin: *\r\n"
        "Connection: close\r\n"
        "Content-Length: {}\r\n"
        "\r\n"
    ).format(len(body)).encode("utf-8")

    client.write(header)
    client.write(body)

def send_not_found(client):
    body = b"404 Not Found"
    header = (
        "HTTP/1.1 404 Not Found\r\n"
        "Content-Type: text/plain\r\n"
        "Connection: close\r\n"
        "Content-Length: {}\r\n"
        "\r\n"
    ).format(len(body)).encode("utf-8")

    client.write(header)
    client.write(body)

# =========================
# サーバ開始
# =========================
ip = connect_wifi()

addr = socket.getaddrinfo("0.0.0.0", 80)[0][-1]
server = socket.socket()
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind(addr)
server.listen(1)
server.setblocking(False)

print("HTTP server started")
print("Open: http://{}/turn".format(ip))

# =========================
# メインループ
# =========================
while True:
    update_turn_state()

    client = None
    try:
        client, client_addr = server.accept()
        client.setblocking(True)

        req = client.recv(1024)
        if not req:
            client.close()
            client = None
            continue

        first_line = req.decode("utf-8").split("\r\n")[0]
        print("request:", first_line)

        parts = first_line.split(" ")
        path = "/"
        if len(parts) >= 2:
            path = parts[1]

        if path == "/turn":
            send_turn_response(client, latest_turn)
            latest_turn = "none"
        else:
            send_not_found(client)

    except OSError:
        # 非ブロッキング accept で接続がないときに来る
        pass
    except Exception as e:
        print("client error:", e)
    finally:
        if client is not None:
            try:
                client.close()
            except:
                pass

    time.sleep(0.02)