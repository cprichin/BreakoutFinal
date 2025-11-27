Imports Microsoft.VisualBasic.Devices
Imports WMPLib


' Music :
' "Galactic Rap"
' Kevin MacLeod (incompetech.com)
' Licensed under Creative Commons: By Attribution 4.0
' https://creativecommons.org/licenses/by/4.0/


Public Class Form1
    Private paddleRect As Rectangle
    Private ballPos As PointF
    Private ballVel As PointF
    Private ballSize As Integer = 14

    Private bricks As List(Of Rectangle)
    Private brickRows As Integer = 5
    Private brickCols As Integer = 10
    Private brickHeight As Integer = 20
    Private brickMargin As Integer = 5

    Private paddleWidth As Integer = 100
    Private paddleHeight As Integer = 15
    Private moveLeft As Boolean
    Private moveRight As Boolean

    Private currTime As Integer = 0
    Private bestTimeEasy As Integer = 0
    Private bestTimeMedium As Integer = 0
    Private bestTimeHard As Integer = 0

    Private musicPlayer As New WindowsMediaPlayer()

    Private timeShown As New Label() With {
        .Location = New Point(10, 30),
        .ForeColor = Color.White,
        .BackColor = Color.Transparent,
        .Visible = False,
        .AutoSize = True
    }
    Private bestTimeShown As New Label() With {
        .Location = New Point(10, 10),
        .ForeColor = Color.White,
        .BackColor = Color.Transparent,
        .Visible = False,
        .AutoSize = True
    }

    Private diff As Integer = -1 ' -1 = none selected; 0 easy; 1 medium; 2 hard
    Private speedMult As Decimal
    Private score As Integer
    Private running As Boolean
    Private gameTimer As Timer
    Private runTimer As Timer
    Private startButton As New Button() With {.Text = "Start Game", .Size = New Size(200, 100)}
    Private inMenu As Boolean = True
    Private rnd As New Random()

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim musicPath As String = IO.Path.Combine(Application.StartupPath, "galactic_rap.mp3")
        If IO.File.Exists(musicPath) Then
            musicPlayer.URL = musicPath
            musicPlayer.settings.setMode("loop", True)
            musicPlayer.controls.play()
        End If
        Me.Text = "Breakout !"


        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.DoubleBuffered = True
        Me.KeyPreview = True

        Controls.Add(startButton)
        Controls.Add(timeShown)
        Controls.Add(bestTimeShown)
        AddHandler startButton.Click, AddressOf startButton_Click

        StartMenu()
    End Sub

    Private Function formatTime(ByVal Time As Integer) As String
        Dim minutes As Integer = Time \ 60
        Dim seconds As Integer = Time Mod 60
        Return String.Format("{0:D2}:{1:D2}", minutes, seconds)
    End Function

    Private Function GetCurrentBestTime() As Integer
        Select Case diff
            Case 0 : Return bestTimeEasy
            Case 1 : Return bestTimeMedium
            Case 2 : Return bestTimeHard
            Case Else : Return 0
        End Select
    End Function

    Private Sub SetBestTimeIfBetter()
        Dim bt = GetCurrentBestTime()
        If bt = 0 OrElse currTime < bt Then
            Select Case diff
                Case 0 : bestTimeEasy = currTime
                Case 1 : bestTimeMedium = currTime
                Case 2 : bestTimeHard = currTime
            End Select
        End If
    End Sub

    Private Sub UpdateBestTimeLabel()
        Dim bt = GetCurrentBestTime()
        Dim prefix = If(diff = -1, "Best time (select difficulty) : ", "Best time : ")
        bestTimeShown.Text = prefix & If(bt = 0, "--:--", formatTime(bt))
    End Sub

    Private Sub CenterButtons()
        If startButton Is Nothing Then Return
        startButton.Location = New Point(
            (ClientSize.Width - startButton.Width) \ 2,
            (ClientSize.Height - startButton.Height) \ 2
        )
    End Sub

    Private Sub StartMenu()
        inMenu = True
        ClearScreen()
        CenterButtons()
        startButton.Visible = True
        timeShown.Visible = False
        bestTimeShown.Visible = True
        UpdateBestTimeLabel()
        Invalidate()
    End Sub

    Private Sub startButton_Click(sender As Object, e As EventArgs)
        DifficultySelect()
    End Sub

    Private Sub DifficultySelect()
        Dim difficultyForm As New Form() With {
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .Text = "Select Difficulty",
            .Size = New Size(300, 200),
            .StartPosition = FormStartPosition.CenterParent
        }

        Dim easyButton As New Button() With {.Text = "Easy", .Size = New Size(80, 30), .Location = New Point(30, 50)}
        Dim mediumButton As New Button() With {.Text = "Medium", .Size = New Size(80, 30), .Location = New Point(110, 50)}
        Dim hardButton As New Button() With {.Text = "Hard", .Size = New Size(80, 30), .Location = New Point(190, 50)}

        AddHandler easyButton.Click, Sub()
                                         brickRows = 4 : brickCols = 8 : paddleWidth = 120 : speedMult = 1D : diff = 0
                                         DifficultySelected()
                                         difficultyForm.Close()

                                     End Sub
        AddHandler mediumButton.Click, Sub()
                                           brickRows = 5 : brickCols = 10 : paddleWidth = 100 : speedMult = 1.05D : diff = 1
                                           DifficultySelected()
                                           difficultyForm.Close()

                                       End Sub
        AddHandler hardButton.Click, Sub()
                                         brickRows = 6 : brickCols = 12 : paddleWidth = 80 : speedMult = 1.1D : diff = 2
                                         DifficultySelected()
                                         difficultyForm.Close()

                                     End Sub

        difficultyForm.Controls.AddRange({easyButton, mediumButton, hardButton})
        difficultyForm.ShowDialog(Me)
    End Sub

    Private Sub DifficultySelected()
        startButton.Visible = False
        inMenu = False
        InitTimers()
        UpdateBestTimeLabel()
        StartGame()
    End Sub

    Private Sub InitTimers()
        gameTimer = New Timer() With {.Interval = 8} ' ~60 FPS
        AddHandler gameTimer.Tick, AddressOf GameLoop

        runTimer = New Timer() With {.Interval = 1000} ' 1 second
        AddHandler runTimer.Tick, AddressOf RunTimer_Tick
    End Sub

    Private Sub RunTimer_Tick(sender As Object, e As EventArgs)
        If Not running Then Return
        currTime += 1
        timeShown.Text = "Current time : " & formatTime(currTime)
        bestTimeShown.Text = "Best time : " & If(GetCurrentBestTime() = 0, "--:--", formatTime(GetCurrentBestTime()))
        If currTime Mod 5 = 0 Then
            IncreaseBallSpeed(speedMult)
            musicPlayer.SpeedRatio *= speedMult
        End If
    End Sub
    Private Sub IncreaseBallSpeed(scale As Single)
        ' Normalize current velocity and reapply scaled magnitude
        Dim vx = ballVel.X
        Dim vy = ballVel.Y
        Dim mag As Single = Math.Sqrt(vx * vx + vy * vy)
        If mag <= 0.0001F Then Return
        Dim newMag = mag * speedMult
        ballVel.X = vx / mag * newMag
        ballVel.Y = vy / mag * newMag
    End Sub
    Private Sub StartGame()
        moveLeft = False
        moveRight = False
        score = 0
        currTime = 0
        running = True
        timeShown.Visible = True
        bestTimeShown.Visible = True
        CreateBricks()
        paddleRect = New Rectangle((ClientSize.Width - paddleWidth) \ 2, ClientSize.Height - 50, paddleWidth, paddleHeight)
        ballPos = New PointF(ClientSize.Width / 2.0F, ClientSize.Height / 2.0F)
        ballVel = New PointF(4.0F * If(rnd.Next(2) = 0, 1, -1), -5.0F)
        timeShown.Text = "Current time : 00:00"
        UpdateBestTimeLabel()
        gameTimer.Start()
        runTimer.Start()
        Invalidate()
    End Sub

    Private Sub CreateBricks()
        bricks = New List(Of Rectangle)()
        Dim totalMarginSpace = (brickCols + 1) * brickMargin
        Dim brickWidth As Integer = (ClientSize.Width - totalMarginSpace) \ brickCols
        For r = 0 To brickRows - 1
            For c = 0 To brickCols - 1
                Dim x = brickMargin + c * (brickWidth + brickMargin)
                Dim y = 40 + brickMargin + r * (brickHeight + brickMargin)
                bricks.Add(New Rectangle(x, y, brickWidth, brickHeight))
            Next
        Next
    End Sub

    Private Sub GameLoop(sender As Object, e As EventArgs)
        If Not running Then Return

        Dim paddleSpeed = 8
        If moveLeft Then paddleRect.X = Math.Max(0, paddleRect.X - paddleSpeed)
        If moveRight Then paddleRect.X = Math.Min(ClientSize.Width - paddleRect.Width, paddleRect.X + paddleSpeed)

        ballPos.X += ballVel.X
        ballPos.Y += ballVel.Y

        If ballPos.X <= 0 Then ballPos.X = 0 : ballVel.X = -ballVel.X
        If ballPos.X + ballSize >= ClientSize.Width Then ballPos.X = ClientSize.Width - ballSize : ballVel.X = -ballVel.X
        If ballPos.Y <= 0 Then ballPos.Y = 0 : ballVel.Y = -ballVel.Y

        Dim ballRect = New Rectangle(CInt(ballPos.X), CInt(ballPos.Y), ballSize, ballSize)
        If ballRect.IntersectsWith(paddleRect) AndAlso ballVel.Y > 0 Then
            ballPos.Y = paddleRect.Y - ballSize
            ballVel.Y = -ballVel.Y
            Dim hitPos As Single = (ballRect.Center().X - paddleRect.X) / paddleRect.Width - 0.5F
            ballVel.X += hitPos * 4.0F

        End If

        For i = 0 To bricks.Count - 1
            If ballRect.IntersectsWith(bricks(i)) Then
                bricks.RemoveAt(i)
                score += 1
                ballVel.Y = -ballVel.Y
                Exit For
            End If
        Next

        If bricks.Count = 0 Then
            gameWin()
            paddleSpeed = 0
        ElseIf ballPos.Y > ClientSize.Height Then
            gameOver()
            paddleSpeed = 0
        End If

        Invalidate()
    End Sub

    Private Sub gameWin()
        running = False
        gameTimer.Stop()
        runTimer.Stop()
        SetBestTimeIfBetter()
        UpdateBestTimeLabel()
        Dim result = MessageBox.Show("You Win! Time: " & formatTime(currTime) & vbCrLf & "Play Again?", "You Win!", MessageBoxButtons.YesNo)
        If result = DialogResult.Yes Then
            StartGame()
        Else
            StartMenu()
        End If
    End Sub

    Private Sub gameOver()
        running = False
        gameTimer.Stop()
        runTimer.Stop()
        Dim result = MessageBox.Show("Game Over! You broke " & score & " bricks, and you survived for " & formatTime(currTime) & "!" & vbCrLf & "Play Again?", "Game Over", MessageBoxButtons.YesNo)
        If result = DialogResult.Yes Then
            StartGame()
        Else
            StartMenu()
        End If
    End Sub

    Private Sub ClearScreen()
        running = False
        If gameTimer IsNot Nothing Then gameTimer.Stop()
        bricks = Nothing
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        e.Graphics.Clear(Color.Black)
        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias

        If bricks Is Nothing Then Return

        Using paddleBrush As New SolidBrush(Color.DeepSkyBlue)
            e.Graphics.FillRectangle(paddleBrush, paddleRect)
        End Using

        Using ballBrush As New SolidBrush(Color.White)
            e.Graphics.FillEllipse(ballBrush, CInt(ballPos.X), CInt(ballPos.Y), ballSize, ballSize)
        End Using

        For Each b In bricks
            Using brickBrush As New SolidBrush(Color.FromArgb(255, 60 + (b.Y Mod 120), 120, 180))
                e.Graphics.FillRectangle(brickBrush, b)
            End Using
            Using pen As New Pen(Color.Black)
                e.Graphics.DrawRectangle(pen, b)
            End Using
        Next
    End Sub

    Private Sub Form1_KeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
        If e.KeyCode = Keys.Left Then moveLeft = True
        If e.KeyCode = Keys.Right Then moveRight = True

    End Sub

    Private Sub Form1_KeyUp(sender As Object, e As KeyEventArgs) Handles MyBase.KeyUp
        If e.KeyCode = Keys.Left Then moveLeft = False
        If e.KeyCode = Keys.Right Then moveRight = False
    End Sub
End Class

Module RectangleExtensions
    <System.Runtime.CompilerServices.Extension>
    Public Function Center(r As Rectangle) As PointF
        Return New PointF(r.X + r.Width / 2.0F, r.Y + r.Height / 2.0F)
    End Function
End Module
