﻿Imports System.IO
Imports System.Net.Sockets
Imports System.Reflection
Imports System.Text
Imports Microsoft.VisualBasic.Language
Imports SMRUCC.HTTPInternal.AppEngine
Imports SMRUCC.HTTPInternal.AppEngine.POSTParser
Imports SMRUCC.HTTPInternal.Core
Imports SMRUCC.HTTPInternal.Platform.Plugins

Namespace Platform

    ''' <summary>
    ''' 服务基础类，REST API的开发需要引用当前的项目
    ''' </summary>
    Public Class PlatformEngine : Inherits HttpFileSystem

        Public ReadOnly Property AppManager As AppEngine.APPManager
        Public ReadOnly Property TaskPool As New TaskPool
        Public ReadOnly Property EnginePlugins As Plugins.PluginBase()

        ''' <summary>
        ''' Init engine.
        ''' </summary>
        ''' <param name="port"></param>
        ''' <param name="root">html wwwroot</param>
        ''' <param name="nullExists"></param>
        ''' <param name="appDll">Must have a Class object implements the type <see cref="WebApp"/></param>
        Sub New(root As String,
                Optional port As Integer = 80,
                Optional nullExists As Boolean = False,
                Optional appDll As String = "")

            Call MyBase.New(port, root, nullExists)
            Call __init(appDll)
        End Sub

        ''' <summary>
        ''' Scanning the dll file and then load the <see cref="WebApp"/> content.
        ''' </summary>
        ''' <param name="dll"></param>
        Private Sub __init(dll As String)
            dll = FileIO.FileSystem.GetFileInfo(dll).FullName
            _AppManager = New AppEngine.APPManager(Me)

            If dll.FileExists Then
                Call AppEngine.ExternalCall.ParseDll(dll, Me)
                Call __runDll(dll)
            Else
                Call AppEngine.ExternalCall.Scan(Me)
            End If

            Me._EnginePlugins = Plugins.ExternalCall.Scan(Me)
        End Sub

        ''' <summary>
        ''' Call sub main in the <see cref="WebApp"/> dll
        ''' </summary>
        ''' <param name="dll"></param>
        Private Sub __runDll(dll As String)
            Dim assm As Assembly = Assembly.LoadFile(dll)
            Dim types As Type() = assm.GetTypes
            Dim webApp As Type =
                LinqAPI.DefaultFirst(Of Type) <= From type As Type
                                                 In types
                                                 Where String.Equals(type.Name, NameOf(AppEngine.WebApp), StringComparison.OrdinalIgnoreCase)
                                                 Select type

            If webApp Is Nothing Then
                Return     ' 没有定义 Sub Main，则忽略掉这次调用
            End If

            Dim ms = webApp.GetMethods
            Dim main As MethodInfo =
                LinqAPI.DefaultFirst(Of MethodInfo) <= From m As MethodInfo
                                                       In ms
                                                       Where String.Equals(m.Name, "Main", StringComparison.OrdinalIgnoreCase)
                                                       Select m
            If main Is Nothing Then
                Return
            End If

            Dim params = main.GetParameters

            If params.IsNullOrEmpty Then
                Call main.Invoke(Nothing, Nothing)
            Else
                Dim args As Object() = {Me}
                Call main.Invoke(Nothing, args)
            End If
        End Sub

        Const contentType As String = "Content-Type"

        Public Overrides Sub handlePOSTRequest(p As HttpProcessor, inputData As MemoryStream)
            Dim out As String = ""
            Dim args As New PostReader(inputData, p.httpHeaders(contentType), Encoding.UTF8)
            Dim success As Boolean = AppManager.InvokePOST(p.http_url, args, out)

            Call __handleSend(p, success, out)
        End Sub

        ''' <summary>
        ''' GET
        ''' </summary>
        ''' <param name="p"></param>
        Protected Overrides Sub __handleREST(p As HttpProcessor)
            Dim out As String = ""
            Dim success As Boolean = AppManager.Invoke(p.http_url, out)
            Call __handleSend(p, success, out)
        End Sub

        Private Sub __handleSend(p As HttpProcessor, success As Boolean, out As String)
            Call p.outputStream.WriteLine(out)

            For Each plugin As PluginBase In EnginePlugins
                Call plugin.handleVisit(p, success)
            Next
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            For Each plugin As Plugins.PluginBase In EnginePlugins
                Call plugin.Dispose()
            Next
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace