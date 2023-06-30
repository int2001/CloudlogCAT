Public Class Form1
    ' List of Cloudlog Targets
    Dim targets As ArrayList
    Dim targetsStr As String ' Strore the settings string to detect changes
    'OmniRig events WithEvents
    Dim WithEvents OmniRigEngine As OmniRig.OmniRigX
    Dim Rig As OmniRig.RigX
    Dim OurRigNo As Integer
    Dim rig1name As String
    Dim rig2name As String

    'Thread-Safe Calls To Windows Forms Controls
    Delegate Sub ShowRigStatusDelegate()
    Delegate Sub ShowRigParamsDelegate()

    ' Constants for enum RigParamX
    Const PM_UNKNOWN = &H1
    Const PM_FREQ = &H2
    Const PM_FREQA = &H4
    Const PM_FREQB = &H8
    Const PM_PITCH = &H10
    Const PM_RITOFFSET = &H20
    Const PM_RIT0 = &H40
    Const PM_VFOAA = &H80
    Const PM_VFOAB = &H100
    Const PM_VFOBA = &H200
    Const PM_VFOBB = &H400
    Const PM_VFOA = &H800
    Const PM_VFOB = &H1000
    Const PM_VFOEQUAL = &H2000
    Const PM_VFOSWAP = &H4000
    Const PM_SPLITON = &H8000
    Const PM_SPLITOFF = &H10000
    Const PM_RITON = &H20000
    Const PM_RITOFF = &H40000
    Const PM_XITON = &H80000
    Const PM_XITOFF = &H100000
    Const PM_RX = &H200000
    Const PM_TX = &H400000
    Const PM_CW_U = &H800000
    Const PM_CW_L = &H1000000
    Const PM_SSB_U = &H2000000
    Const PM_SSB_L = &H4000000
    Const PM_DIG_U = &H8000000
    Const PM_DIG_L = &H10000000
    Const PM_AM = &H20000000
    Const PM_FM = &H40000000

    ' Constants for enum RigStatusX
    Const ST_NOTCONFIGURED = &H0
    Const ST_DISABLED = &H1
    Const ST_PORTBUSY = &H2
    Const ST_NOTRESPONDING = &H3
    Const ST_ONLINE = &H4

    Public Sub loadSettings()
        ' only do the deserialization if something has changed, a hint of better performance
        If Not String.Equals(My.Settings.MultiCloudlog, Me.targetsStr) Then
            ' do the deserialization
            Me.targets = CloudlogTarget.fromSettingsString(My.Settings.MultiCloudlog)
            Me.targetsStr = My.Settings.MultiCloudlog
        End If
    End Sub


    'Form LOAD events
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load

        ' Set Status Bar Information
        L_CL_STATE.Text = "Cloudlog Not Connected"

        RadioButton1.Checked = True

        If My.Settings.TransverterEnabled = True Then
            TransverterOffsetToolStripMenuItem.Checked = True
        End If

        ' Check weather we need to adjust the settings
        If String.Equals(My.Settings.MultiCloudlog, "") Then
            ' There are no Multi Cloudlog Entrys, create one

            Dim target As New CloudlogTarget() ' Initializes the new object with defaults sting and sets it as not active

            ' There are Settings strored in the URL and API, transport them (leagacy reasons!)
            If Not String.Equals(My.Settings.CloudlogURL, "") Then
                target.url = My.Settings.CloudlogURL
                target.key = My.Settings.CloudlogAPIKey
                target.active = True
                Console.WriteLine(My.Settings.CloudlogURL)
            End If

            ' Store the targets as a list, and save it
            Dim targets As New ArrayList()
            targets.Add(target)
            Me.targets = targets
            My.Settings.MultiCloudlog = CloudlogTarget.toSettingsString(targets)
            Me.targetsStr = My.Settings.MultiCloudlog ' set the string to compoare against changes
            My.Settings.Save()
        Else
            Me.targetsStr = "" ' force a load of objects
            loadSettings() ' deserialize the string
        End If


        StartOmniRig()
    End Sub

    Private Sub Form1_FormClosed(sender As Object, e As FormClosedEventArgs)
        StopOmniRig()
    End Sub

    'Thread-Safe Calls To Windows Forms Controls
    'OmniRig StatusChange events
    Private Sub OmniRigEngine_StatusChange(ByVal RigNumber As Long) Handles OmniRigEngine.StatusChange
        If RigNumber = OurRigNo Then Invoke(New ShowRigStatusDelegate(AddressOf ShowRigStatus))
    End Sub

    'Thread-Safe Calls To Windows Forms Controls
    'OmniRig ParamsChange events
    Private Sub OmniRigEngine_ParamsChange(ByVal RigNumber As Long, ByVal Params As Long) Handles OmniRigEngine.ParamsChange
        If RigNumber = OurRigNo Then Invoke(New ShowRigParamsDelegate(AddressOf ShowRigParams))
    End Sub

    'Thread-Safe Calls To Windows Forms Controls
    Private Sub ShowRigStatus()
        If Rig Is Nothing Then Exit Sub
        Label8.Text = Rig.StatusStr
        Label2.Text = Rig.RigType
    End Sub

    'Thread-Safe Calls To Windows Forms Controls
    Private Sub ShowRigParams()


        If Rig Is Nothing Then Exit Sub

        Dim newfreq As String
        Dim F_RX_A As ULong = Math.Round(CDbl(Rig.GetRxFrequency) / 1000, 6)

        If (My.Settings.TransverterEnabled = True) Then
            If (My.Settings.TransverterOffsetType = "Plus") Then
                newfreq = Rig.GetRxFrequency + My.Settings.TransverterFreq
            Else
                newfreq = Rig.GetRxFrequency - My.Settings.TransverterFreq
            End If

            Label3.Text = Math.Round(CDbl(newfreq) / 1000, 6)
        Else
            newfreq = Rig.GetRxFrequency
            Label3.Text = Rig.GetRxFrequency / 1000000.ToString
        End If

        If F_RX_A < 10000 Then
            Label3.Text = Format(CSng(Label3.Text), "0.00000")
        Else
            Label3.Text = Format(CSng(Label3.Text), "00.00000")
        End If

        Select Case Rig.Mode
            Case PM_CW_L
                Label6.Text = "CW"
            Case PM_CW_U
                Label6.Text = "CW"
            Case PM_SSB_L
                Label6.Text = "LSB"
            Case PM_SSB_U
                Label6.Text = "USB"
            Case PM_DIG_U
                Label6.Text = "DIGI"
            Case PM_DIG_L
                Label6.Text = "DIGI"
            Case PM_AM
                Label6.Text = "AM"
            Case PM_FM
                Label6.Text = "FM"
            Case Else
                Label6.Text = "Other"
        End Select

        'Connect to Cloudlog API and sync frequency
        loadSettings() ' surely there is a better way to update the settings... but it works!


        Dim allOK As Boolean
        Dim anyTarget As Boolean
        allOK = True ' If any of the targets fail, we need to know
        anyTarget = False ' Additional flag, if any target is supposed to be active

        Dim timestamp As String = DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss")


        For Each target As CloudlogTarget In targets
            ' upload each to target, if requested and possible
            If target.active And Not String.Equals(target.url, "") Then
                anyTarget = True
                Try
                    Using client As New Net.WebClient
                        Net.ServicePointManager.SecurityProtocol = Net.SecurityProtocolType.Tls12

                        Dim RadioName As String = ""

                        ' The radio name will be populated my the target object 
                        If RadioButton1.Checked Then
                            RadioName = "1"
                        Else
                            RadioName = "2"
                        End If


                        ' build string
                        Dim myString As String = "{""radio"": """ + target.radioName + " " + RadioName + """, ""frequency"": """ + newfreq.ToString + """, ""mode"": """ + Label6.Text + """, ""timestamp"": """ + timestamp + """, ""key"": """ + target.key + """}"

                        Try
                            Dim responsebytes = client.UploadString(target.url + "/index.php/api/radio", myString)
                        Catch ex As Exception
                            If allOK = True Then ' only display error for the first entry, maybe room for improvement
                                L_CL_STATE.Text = "Cloudlog Synced for " + target.name + ": Failed, check URL/API"
                                allOK = False
                            End If
                        End Try
                    End Using
                Catch ex As System.Net.WebException
                    If allOK = True Then
                        L_CL_STATE.Text = "Cloudlog Synced for " + target.name + " Failed"
                        allOK = False
                    End If
                End Try

            End If
        Next


        If Not anyTarget Then ' haven't even tried, display an error
            L_CL_STATE.Text = "Cloudlog Synced: Failed, no target active"
        ElseIf allOK Then
            L_CL_STATE.Text = "Cloudlog Synced: " + timestamp ' all targets uploaded sucessfully!
        End If

    End Sub

    'Procedures
    Private Sub StartOmniRig()
        ' On Error GoTo Error1
        If Not OmniRigEngine Is Nothing Then Exit Sub
        OmniRigEngine = CreateObject("OmniRig.OmniRigX")
        ' we want OmniRig interface V.1.1 to 1.99
        ' as V2.0 will likely be incompatible  with 1.x
        If OmniRigEngine.InterfaceVersion < &H101 Then GoTo Error1
        If OmniRigEngine.InterfaceVersion > &H299 Then GoTo Error1
        SelectRig(1)
        Exit Sub
Error1:
        ' report problems
        OmniRigEngine = Nothing
        MsgBox("OmniRig Is Not installed Or has a wrong version number")

    End Sub

    Private Sub StopOmniRig()
        Rig = Nothing
        OmniRigEngine = Nothing
    End Sub

    Private Sub SelectRig(NewRigNo As Integer)
        If OmniRigEngine Is Nothing Then Exit Sub
        OurRigNo = NewRigNo
        Select Case NewRigNo
            Case 1
                Rig = OmniRigEngine.Rig1
            Case 2
                Rig = OmniRigEngine.Rig2
        End Select
        ShowRigStatus()
        ShowRigParams()
    End Sub

    Private Sub RadioButton1_Click(sender As Object, e As EventArgs) Handles RadioButton1.Click
        SelectRig(1)
    End Sub

    Private Sub RadioButton2_Click(sender As Object, e As EventArgs) Handles RadioButton2.Click
        SelectRig(2)
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs)
        Dim freq As Integer
        Rig.SetSimplexMode(freq)
    End Sub


    Private Sub OmnirigToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles OmnirigToolStripMenuItem.Click
        If OmniRigEngine Is Nothing Then Exit Sub
        OmniRigEngine.DialogVisible = True
    End Sub

    Private Sub ExitToolStripMenuItem_Click(sender As Object, e As EventArgs)
        Close()
    End Sub

    Private Sub CloudlogToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles CloudlogToolStripMenuItem.Click
        CloudlogSettingsForm.Show()
    End Sub

    Private Sub AboutToolStripMenuItem_Click(sender As Object, e As EventArgs)
        'AboutBox1.Show()
    End Sub

    Private Sub TransverterOffsetToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles TransverterOffsetToolStripMenuItem.Click
        Form3.Show()
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Close()
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        WindowState = FormWindowState.Minimized
    End Sub


    Private isDragging As Boolean = False
    Private clickOffset As Point

    Private Sub Form1_MouseDown(sender As Object, e As MouseEventArgs) Handles MyBase.MouseDown
        If e.Button = MouseButtons.Left Then
            isDragging = True
            clickOffset = New Point(e.X, e.Y)
        End If
    End Sub

    Private Sub Form1_MouseMove(sender As Object, e As MouseEventArgs) Handles MyBase.MouseMove
        If isDragging Then
            Dim newLocation As Point = Me.Location
            newLocation.X += e.X - clickOffset.X
            newLocation.Y += e.Y - clickOffset.Y
            Me.Location = newLocation
        End If
    End Sub

    Private Sub Form1_MouseUp(sender As Object, e As MouseEventArgs) Handles MyBase.MouseUp
        If e.Button = MouseButtons.Left Then
            isDragging = False
        End If
    End Sub

    Private Sub AlwaysOnTopToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AlwaysOnTopToolStripMenuItem.Click
        AlwaysOnTopToolStripMenuItem.Checked = Not AlwaysOnTopToolStripMenuItem.Checked
        TopMost = AlwaysOnTopToolStripMenuItem.Checked
    End Sub
    Private Sub AboutToolStripMenuItem_Click_1(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem.Click
        AboutBox1.Show()
    End Sub

End Class
