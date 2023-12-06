# Com2

简单易用的串口转发工具，可以在一个实例之中打开两个串口，通过Lua脚本进行条件判断，实现从一个串口将数据按条件，或做格式转换，转发到另一个串口的功能。

代码是在llcom之上做了简化，删去了大部分功能只保留最基本的脚本解释和串口功能。

[llcom: 功能强大的串口工具。支持Lua自动化处理、串口调试、串口监听、串口曲线、TCP测试、MQTT测试、编码转换、乱码恢复等功能 (gitee.com)](https://gitee.com/chenxuuu/llcom)



代码编译时，需将LuaScript下的core_script和script目录复制到bin/Debug或bin/Release下，程序启动时需加载lua脚本初始化，同时用户编写的lua脚本也放在script目录。



lua脚本语言的基本语法可以在网上自行搜索相关教程。

下面为Com2之中使用lua脚本获取和发送串口数据的简单介绍：

```lua
--lua语言注释以--开始，函数以end结束  if语句格式为 if ... then ... elseif ... then ... else ... end
--字符串连接使用..
--print为输出调试信息
print("脚本启动成功")

i = 0
--uart0为串口0  此处代码为接受串口0数据并进行处理 apisetcb有两个参数，第一个为串口名，第二个为对数据处理的匿名函数
apiSetCb("uart0", function (data)
    print("uart0 receive: ",data)
    --此处为进行条件判断并处理
    if data == "yes" then
        data =  data .. " - " .. i
        i = i + 1
    elseif data == "no" then
        --
    else
        --
    end   

    --此函数为发送数据函数，向uart1发送数据，返回true为成功
    local sendResult = apiSend("uart1", data)
    print("uart1 send:", data, " result - ", sendResult)   
end)

apiSetCb("uart1", function (data)
    print("uart1 receive: ",data)
    --发送串口消息，并获取发送结果
    if data == "yes" then
        data =  data .. " - " .. i
        i = i + 1
        local sendResult = apiSend("uart0", data)
        print("uart1 send:", data, " result - ", sendResult)  
    end 
end)

```



