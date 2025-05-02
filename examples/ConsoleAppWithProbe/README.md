# How to test

```shell
sudo lsof -i TCP:5555

COMMAND     PID                USER   FD   TYPE             DEVICE SIZE/OFF NODE NAME
ConsoleAp 45948 usernamexxxxxxxxxxxx  165u  IPv4 0x600000000000000      0t0  TCP *:personal-agent (LISTEN)
```

If you get a result, the app is running and healthy.

The TCP listener is configured to stop listening if the health check status is not healthy: you'll get no result. 