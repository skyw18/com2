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

    else

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


-- 延时等待函数 需配合 sys.publish("UART",data)--发布消息
-- sys.taskInit(function()
--     local i = 0
--     while true do
--         --等待消息，超时1000ms
--         local r,udata = sys.waitUntil("UART0",1000)

--         if r then
--             log.info("uart wait" , r , udata)

--             --发送串口消息，并获取发送结果
--             if udata == "yes" then
--                 local sendResult = apiSend("uart0", udata .. " - " .. i)
--                 log.info("uart send",sendResult)
--                 i = i + 1
--             end            
--         end
--     end
-- end)