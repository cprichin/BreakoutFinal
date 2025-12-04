Imports Microsoft.VisualBasic.Devices
Imports WMPLib


' Music :
' "Galactic Rap"
' Kevin MacLeod (incompetech.com)
' Licensed under Creative Commons: By Attribution 4.0
' https://creativecommons.org/licenses/by/4.0/


Public Class Form1

    Private lifeCount As New Label() With {
        .Location = New Point(
                Math.Max(5, ClientSize.Width - .Width - 10),
                Math.Max(5, ClientSize.Height - .Height - 10)
            ),
        .ForeColor = Color.White,
        .BackColor = Color.Transparent,
        .Text = "Lives Left: --",
        .Visible = False,
        .AutoSize = True
    }
    Private statusLabel As New Label() With {
        .Location = New Point(
            (ClientSize.Width - .Width) \ 2,
            (ClientSize.Height - .Height) \ 2
        ),
        .ForeColor = Color.Yellow,
        .BackColor = Color.Transparent,
        .Visible = False,
        .AutoSize = True
    }

    Private lives As Integer = 3
    Private readyTimer As Timer
    Private countdown As Integer = 0

    Private Const BASE_SPEED As Single = 5.0F

    Private paddleRect As Rectangle
    Private ballPos As PointF
    Private ballVel As PointF
    Private ballSize As Integer = 14

    Private bricks As List(Of Brick)
    Public brickRows As Integer = 5
    Public brickCols As Integer = 10
    Private brickHeight As Integer = 20
    Private brickMargin As Integer = 5
    Public totalHits As Integer = 0

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

    Public diff As Integer = -1 ' -1 = none selected; 0 easy; 1 medium; 2 hard
    Private speedMult As Decimal
    Private score As Integer
    Private running As Boolean
    Private gameTimer As Timer
    Private runTimer As Timer
    Private startButton As New Button() With {.Text = "Start Game", .Size = New Size(200, 100)}
    Private inMenu As Boolean = True
    Private rnd As New Random()

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim musicPath As String = IO.Path.Combine(Application.StartupPath, "Assets", "galactic_rap.mp3")
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
        Controls.Add(statusLabel)
        Controls.Add(lifeCount)

        AddHandler startButton.Click, AddressOf startButton_Click

        StartMenu()
    End Sub

    Private Sub ResetBallAndPaddle()
        paddleRect = New Rectangle((ClientSize.Width - paddleWidth) \ 2,
                                   ClientSize.Height - 50,
                                   paddleWidth,
                                   paddleHeight)

        ballPos = New PointF(ClientSize.Width / 2.0F, ClientSize.Height / 2.0F)
        ballVel = New PointF(4.0F * If(rnd.Next(2) = 0, 1, -1), -BASE_SPEED)
    End Sub

    Private Sub ReadyTimer_Tick(sender As Object, e As EventArgs)
        countdown -= 1

        If countdown > 0 Then
            statusLabel.Text = "Get ready: " & countdown.ToString()
        Else
            readyTimer.Stop()
            statusLabel.Visible = False

            running = True
            gameTimer.Start()
            runTimer.Start()
        End If
    End Sub

    Private Sub LoseLife()
        running = False
        If gameTimer IsNot Nothing Then gameTimer.Stop()
        If runTimer IsNot Nothing Then runTimer.Stop()

        lives -= 1
        lifeCount.Text = "Lives Left: " & lives.ToString()
        If lives > 0 Then
            ResetBallAndPaddle()
            countdown = 2
            statusLabel.Text = "Life lost! Lives left: " & lives.ToString()
            statusLabel.Visible = True
            readyTimer.Start()
            Invalidate()
        Else
            gameOver()
        End If
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
        gameTimer = New Timer() With {.Interval = 8} ' ~60 FPS
        AddHandler gameTimer.Tick, AddressOf GameLoop
        readyTimer = New Timer() With {.Interval = 1000}
        AddHandler readyTimer.Tick, AddressOf ReadyTimer_Tick

        runTimer = New Timer() With {.Interval = 1000} ' 1 second
        AddHandler runTimer.Tick, AddressOf RunTimer_Tick
        UpdateBestTimeLabel()
        StartGame()
    End Sub

    Private Sub RunTimer_Tick(sender As Object, e As EventArgs)
        If Not running Then Return
        currTime += 1
        timeShown.Text = "Current time : " & formatTime(currTime)
        bestTimeShown.Text = "Best time : " & If(GetCurrentBestTime() = 0, "--:--", formatTime(GetCurrentBestTime()))
        If currTime Mod 5 = 0 Then
            IncreaseBallSpeed(speedMult)
            musicPlayer.settings.rate *= speedMult
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
        statusLabel.Location = New Point(
            (ClientSize.Width - statusLabel.Width) \ 2 - 45,
            (ClientSize.Height - statusLabel.Height) \ 2 - 25
        )
        statusLabel.Visible = True
        lifeCount.Location = New Point(ClientSize.Width - lifeCount.Width - 10, ClientSize.Height - lifeCount.Height - 10)
        lifeCount.Visible = True
        musicPlayer.settings.rate = 1
        moveLeft = False
        moveRight = False
        score = 0
        currTime = 0

        running = True
        timeShown.Visible = True
        bestTimeShown.Visible = True
        CreateBricks()
        ResetBallAndPaddle()
        lives = 3
        running = False
        countdown = 3
        statusLabel.Text = "Get ready: " & countdown.ToString()
        statusLabel.Visible = True

        If gameTimer IsNot Nothing Then gameTimer.Stop()
        If runTimer IsNot Nothing Then runTimer.Stop()
        readyTimer.Start()

        timeShown.Text = "Current time : 00:00"
        UpdateBestTimeLabel()
        gameTimer.Start()
        runTimer.Start()
        Invalidate()
    End Sub

    Private Sub CreateBricks()
        bricks = New List(Of Brick)()
        Dim totalMarginSpace = (brickCols + 1) * brickMargin
        Dim brickWidth As Integer = (ClientSize.Width - totalMarginSpace) \ brickCols
        For r = 0 To brickRows - 1
            For c = 0 To brickCols - 1
                Dim x = brickMargin + c * (brickWidth + brickMargin)
                Dim y = 40 + brickMargin + r * (brickHeight + brickMargin)
                bricks.Add(New Brick(New Rectangle(x, y, brickWidth, brickHeight), r))
            Next
        Next
    End Sub

    Private Sub GameLoop(sender As Object, e As EventArgs)
        If Not running Then Return
        lifeCount.Text = "Lives Left: " & lives.ToString()
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
            If ballRect.IntersectsWith(bricks(i).Bounds) Then
                Dim curr As Rectangle = bricks(i).Bounds

                Dim overlapLeft = ballRect.Right - curr.Left
                Dim overlapRight = curr.Right - ballRect.Left
                Dim overlapTop = ballRect.Bottom - curr.Top
                Dim overlapBottom = curr.Bottom - ballRect.Top

                Dim minX = Math.Min(overlapLeft, overlapRight)
                Dim minY = Math.Min(overlapTop, overlapBottom)

                If minX < minY Then
                    If overlapLeft < overlapRight Then
                        ballPos.X = curr.Left - ballRect.Width
                        ballVel.X = -Math.Abs(ballVel.X)
                    Else
                        ballPos.X = curr.Right
                        ballVel.X = Math.Abs(ballVel.X)
                    End If
                Else
                    If overlapTop < overlapBottom Then
                        ballPos.Y = curr.Top - ballRect.Height
                        ballVel.Y = -Math.Abs(ballVel.Y)
                    Else
                        ballPos.Y = curr.Bottom
                        ballVel.Y = Math.Abs(ballVel.Y)
                    End If
                End If
                If bricks(i).hitCount > 1 Then
                    bricks(i).hitCount -= 1
                Else
                    bricks.RemoveAt(i)
                End If
                score += 1
                Exit For
            End If
        Next
        lifeCount.BringToFront()
        If bricks.Count = 0 Then
            gameWin()
            paddleSpeed = 0
        ElseIf ballPos.Y > ClientSize.Height Then
            LoseLife()
            paddleSpeed = 0
        End If

        Invalidate()
    End Sub

    Private Sub gameWin()
        running = False
        totalHits = 0
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
        totalHits = 0
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
        If runTimer IsNot Nothing Then runTimer.Stop()
        If readyTimer IsNot Nothing Then readyTimer.Stop()
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

        For Each b As Brick In bricks
            Using brickBrush As New SolidBrush(b.DisplayColor)
                e.Graphics.FillRectangle(brickBrush, b.Bounds)
            End Using
            Using pen As New Pen(Color.Black)
                e.Graphics.DrawRectangle(pen, b.Bounds)
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

    Private Sub Form1_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        Try
            musicPlayer.controls.stop()
            musicPlayer.close()
        Catch
        End Try
    End Sub
End Class

Module RectangleExtensions
    <System.Runtime.CompilerServices.Extension>
    Public Function Center(r As Rectangle) As PointF
        Return New PointF(r.X + r.Width / 2.0F, r.Y + r.Height / 2.0F)
    End Function
End Module


Public Class Brick
    Private rnd As New Random()
    Public Property Bounds As Rectangle
    Public Property hitCount As Integer = 1
    Public Property hasPower As Boolean = False
    Public ReadOnly Property DisplayColor As Color
        Get
            If hitCount > 1 Then
                Dim intensity = 100 + (Bounds.Y Mod 100)
                Return Color.FromArgb(255, intensity, 60, 40)
            Else
                Return Color.FromArgb(255, 60 + (Bounds.Y Mod 120), 120, 180)
            End If
        End Get
    End Property


    Public Sub New(bounds As Rectangle, rowNum As Integer)
        Me.Bounds = bounds
        If rnd.Next(8 * Form1.diff) = 0 Then hasPower = True
        If (Form1.diff > 0 AndAlso rowNum < Form1.brickRows - rnd.Next(Form1.brickRows) AndAlso Form1.totalHits < Form1.brickRows + Form1.brickCols + (10 * Form1.diff)) Then
            hitCount += 1
            Form1.totalHits += 1
        End If
    End Sub


End Class