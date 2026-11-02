@echo off
chcp 65001 > nul
title Khởi Chạy MailSenderApp - Đề Tài 5 WPF
echo ========================================================
echo   KHỞI ĐỘNG HỆ THỐNG GỬI EMAIL TỰ ĐỘNG (WPF .NET 8)
echo ========================================================
echo Đang mở ứng dụng...

start "" "%~dp0MailSenderApp\bin\Debug\net8.0-windows\MailSenderApp.exe"

exit

