using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Configuration;
using System.Globalization;

namespace BilliardWindowsApplication
{
	public enum TeachingState{Init, Start, Stop, Cancel, Save, Reset, Replay, ShotFinish, Ready, SearchOK, SearchFail};
	public enum LabelState{WrongShotRed, EqualName};
    public partial class FrmTeaching : Form
    {
		public BallTrackAPI.NotifyProcDelegate notifyProc;
		private TeachingState currentState = TeachingState.Init;
		private static int InOutMarkCount = 8;
		private static int RailMarkCount_Horz = 5;
		private static int RailMarkCount_Vert = 9;
		private static bool ForceStopped = false;
		private static int RailMarkCount = (RailMarkCount_Horz + RailMarkCount_Vert) * 2;
		private bool m_bBallFeatureChecked = true;
		PictureBox[] picInOutMarks = new PictureBox[InOutMarkCount];
		TextBox[] txtRailMarks = new TextBox[RailMarkCount];
		TimeSpan starttime = new TimeSpan(0, 0, 0);
		TimeSpan currentTime;
		int slottime1 = 0;
		int slottime2 = 0;
		int slottime3 = 0;
		int slottime4 = 0;
		double dcost = 0.00;
		int m_nMarginX;
		int m_nMarginY;
		float m_fMarginScaleX;
		float m_fMarginScaleY;
		float m_fScaleX;
		float m_fScaleY;
		Rectangle m_rcDrawRect;
		private bool m_bShotFinished = false;
		private bool m_bShotSaved = false;
		Size m_szImageBackground = new Size(523, 950);
		int nReplayPlayer = -1;
		bool m_bUploadMode = false;
		int m_nFuncButton_Upload = 6;
		int m_nFuncBUtton_Download = 4;
		Rectangle[] m_rcFuncButtonUpload;
		Rectangle[] m_rcFuncButtonDownload;
		public static string strPromptNewShotName = "Insert New Shot Name";
		public static string strPromptShotNameForSearch = "";//"Insert Shot Name To Search For";
		public static string strPromptShotDescription = "Description of shot";
		public bool m_bLoginOK = false;
		private bool m_bFirstRun = true;
		int shiftKeyState = 0;
		string currentCost = "0,00 €";
		biliardService.BilliardScoreboard API = new biliardService.BilliardScoreboard();

        public FrmTeaching()
        {
            InitializeComponent();
			CreateMarkControls();
			LayoutTeacherControls();
			LoadTeacherSetting();
			timerDraw.Enabled = false;

			m_rcDrawRect = new Rectangle(0, 0, picBilliardTable.Width, picBilliardTable.Height);
			m_fMarginScaleX = (float)m_rcDrawRect.Width / (float)m_szImageBackground.Width;
			m_fMarginScaleY = (float)m_rcDrawRect.Height / (float)m_szImageBackground.Height;

			m_nMarginX = (int)(50 * m_fMarginScaleX + 0.5f);
			m_nMarginY = (int)(50 * m_fMarginScaleY + 0.5f);

			BallTrackAPI.ShowTeacherPoint(true);
			
			BallTrackAPI.ClearHistory();
			BallTrackAPI.drawProc = new BallTrackAPI.DrawDelegate(DrawPlay);
			BallTrackAPI.stateProc = new BallTrackAPI.StateUpdateDelegate(StateProc);

			UpdateButtonState(TeachingState.Init);
			EnableTeachingControls(false);
			notifyProc = new BallTrackAPI.NotifyProcDelegate(NotifyProc);
			BallTrackAPI.BTAPI_SetNotifyCallback(System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(notifyProc));

            picBallFeature.Image = m_bBallFeatureChecked ? BilliardWindowsApplication.Properties.Resources.white_green_circle : BilliardWindowsApplication.Properties.Resources.circle;

			currentTime = new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0);
        }

		private void CreateMarkControls()
		{
			int i;

			for (i = 0; i < InOutMarkCount; i++)
			{
				picInOutMarks[i] = new PictureBox();
				picInOutMarks[i].Tag = i;
				picInOutMarks[i].Click += picInOut_Click;
			}
			for (i = 0; i < RailMarkCount; i++)
			{
				txtRailMarks[i] = new TextBox();
				txtRailMarks[i].TextAlign = HorizontalAlignment.Center;
				txtRailMarks[i].Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			}

			LayoutTeacherControls();

			for (i = 0; i < InOutMarkCount; i++)
			{
				if (i % 2 == 0)
					picInOutMarks[i].Image = BilliardWindowsApplication.Properties.Resources.red_circle_boundary;
				else
					picInOutMarks[i].Image = BilliardWindowsApplication.Properties.Resources.green_circle_boundary;

				Controls.Add(picInOutMarks[i]);
			}

			for (i = 0; i < RailMarkCount; i++)
			{
				txtRailMarks[i].ForeColor = Color.White;
				txtRailMarks[i].BackColor = Color.Black;
				Controls.Add(txtRailMarks[i]);
			}
		}
		private void LayoutTeacherControls()
		{
			const int nOrgImageWidth = 523;
			const int nOrgImageHeight = 950;

			int nEditWidth = 40;
			int nEditHeight = 26;

			int nDrawImageWidth = picBilliardTable.Width;
			int nDrawImageHeight = picBilliardTable.Height;

			float fScaleX = (float)nDrawImageWidth / (float)nOrgImageWidth;
			float fScaleY = (float)nDrawImageHeight / (float)nOrgImageHeight;

			const int nDefaultMarkStep = 107;
			const int nStartMargin = 48;

			float fArrangeStepX = (float)nDefaultMarkStep * fScaleX;
			float fArrangeStepY = (float)nDefaultMarkStep * fScaleY;

			float fStartMarginX = (float)nStartMargin * fScaleX;
			float fStartMarginY = (float)nStartMargin * fScaleY;

			int nArrangeMarginX = (int)(fStartMarginX);
			int nArrangeMarginY = (int)(fStartMarginY);

			
			int nInOutMarkSize = 24;
			int nInOutMarkDistance = nInOutMarkSize / 2;
			Rectangle[] rcInOutMark = new Rectangle[8];

			Rectangle rcEdit = new Rectangle();
			int nBaseX = picBilliardTable.Location.X;
			int nBaseY = picBilliardTable.Location.Y;
			int nBaseWidth = picBilliardTable.Width;
			int nBaseHeight = picBilliardTable.Height;

			//Top side
			rcEdit.Location = new Point(nBaseX + nArrangeMarginX - nEditWidth / 2, nBaseY - nEditHeight);
			rcEdit.Size = new Size(nEditWidth, nEditHeight);

			rcInOutMark[0].X = picBilliardTable.Location.X + picBilliardTable.Width / 2 - nInOutMarkSize - nInOutMarkDistance / 2;
			rcInOutMark[0].Y = rcEdit.Y - nInOutMarkSize - nInOutMarkDistance / 2;
			rcInOutMark[0].Size = new Size(nInOutMarkSize, nInOutMarkSize);

			rcInOutMark[1] = rcInOutMark[0];
			rcInOutMark[1].Offset(nInOutMarkSize + nInOutMarkDistance, 0);

			int i;

			for (i = 0; i < RailMarkCount_Horz; i++)
			{
				txtRailMarks[i].Bounds = rcEdit;
				rcEdit.Offset((int)fArrangeStepX, 0);
			}

			//=======right side=======
			rcEdit.Location = new Point(nBaseX + nBaseWidth, nBaseY + nArrangeMarginY - nEditHeight / 2);

			rcInOutMark[2].X = rcEdit.Location.X + rcEdit.Width + nInOutMarkDistance / 2;
			rcInOutMark[2].Y = nBaseY + nBaseHeight / 2 - nInOutMarkSize - nInOutMarkDistance / 2;
			rcInOutMark[2].Size = new Size(nInOutMarkSize, nInOutMarkSize);

			rcInOutMark[3] = rcInOutMark[2];
			rcInOutMark[3].Offset(0, nInOutMarkSize + nInOutMarkDistance);

			for (i = 0; i < RailMarkCount_Vert; i++)
			{
				txtRailMarks[RailMarkCount_Horz + i].Bounds = rcEdit;
				rcEdit.Offset(0, (int)(fArrangeStepY));
			}

			//=======bottom side==========
			rcEdit.Location = new Point(nBaseX + nArrangeMarginX - nEditWidth / 2, nBaseY + nBaseHeight);

			rcInOutMark[4].X = rcInOutMark[0].X;
			rcInOutMark[4].Y = nBaseY + nBaseHeight + rcEdit.Height + nInOutMarkDistance / 2;
			rcInOutMark[4].Size = new Size(nInOutMarkSize, nInOutMarkSize);

			rcInOutMark[5] = rcInOutMark[4];
			rcInOutMark[5].Offset(nInOutMarkSize + nInOutMarkDistance, 0);

			for (i = 0; i < RailMarkCount_Horz; i++)
			{
				txtRailMarks[RailMarkCount_Horz + RailMarkCount_Vert + i].Bounds = rcEdit;
				rcEdit.Offset((int)(fArrangeStepX), 0);
			}

			//=======left side==========
			rcEdit.Location = new Point(nBaseX - nEditWidth, nBaseY + nArrangeMarginY - rcEdit.Height / 2);

			rcInOutMark[6].X = nBaseX - rcEdit.Width - nInOutMarkDistance / 2 - nInOutMarkSize;
			rcInOutMark[6].Y = rcInOutMark[2].Y;
			rcInOutMark[6].Size = new Size(nInOutMarkSize, nInOutMarkSize);

			rcInOutMark[7] = rcInOutMark[6];
			rcInOutMark[7].Offset(0, nInOutMarkSize + nInOutMarkDistance);

			for (i = 0; i < RailMarkCount_Vert; i++)
			{
				txtRailMarks[RailMarkCount_Horz * 2 + RailMarkCount_Vert + i].Bounds = rcEdit;
				rcEdit.Offset(0, (int)fArrangeStepY);
			}

			for (i = 0; i < InOutMarkCount; i++)
			{
				picInOutMarks[i].Bounds = rcInOutMark[i];
			}

			txtRailCount.Location = new Point(nBaseX + nBaseWidth / 2 - txtRailCount.Width / 2, nBaseY + nBaseHeight / 2 - txtRailCount.Height / 2);
		}

		private void picBallFeature_Click(object sender, EventArgs e)
		{
			m_bBallFeatureChecked = !m_bBallFeatureChecked;
			picBallFeature.Image = m_bBallFeatureChecked ? BilliardWindowsApplication.Properties.Resources.white_green_circle : BilliardWindowsApplication.Properties.Resources.circle;
			BLL_BilliardWindowsApplication.playclicksound();
            if (m_bUploadMode)
                BallTrackAPI.ShowBallSpeed(m_bBallFeatureChecked);
		}

		private void ShowTeacherControls(bool bShow)
		{
			//int i;
			//for (i = 0; i < InOutMarkCount; i++)
			//	picInOutMarks[i].Visible = bShow;

			//for (i = 0; i < RailMarkCount; i++)
			//	txtRailMarks[i].Visible = bShow;

			txtRailCount.Visible = bShow;
			picBilliardTable.Invalidate();
		}
		private bool IsSetupCompleted()
		{
			if (txtShotName.Text == "" || txtShotName.Text == strPromptNewShotName)
			{
				txtShotName.Focus();
				return false;
			}

			if (txtShotDescription.Text == "" || txtShotDescription.Text == strPromptShotDescription)
			{
				txtShotDescription.Focus();
				return false;
			}

			if (BallTrackAPI.m_nInSide == -1 || BallTrackAPI.m_nOutSide == -1)
				return false;

			if (BallTrackAPI.m_nRailCount <= 0)
			{
				txtRailCount.Focus();
				return false;
			}

			int nMarkSetCount = 0;
			for (int i = 0; i < RailMarkCount; i++ )
			{
				if (txtRailMarks[i] != null && !String.IsNullOrEmpty(txtRailMarks[i].Text))
				{
					nMarkSetCount++;
				}
			}

			if (nMarkSetCount <= 1)
				return false;
			return true;
		}
		private void btnStart_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();
			
			if (BallTrackAPI.m_nInputMethod == 1)
			{
				//BallTrackAPI.BTAPI_OpenVideoFile("D:\\Document\\sample\\BBT\\1006\\1.avi");
				BallTrackAPI.BTAPI_OpenVideoFile("D:\\Document\\sample\\BBT\\seq.avi");
				//BallTrackAPI.BTAPI_OpenVideoFile("8.avi");
			}
			if (m_bUploadMode)
			{
				SaveTeacherSetting();
				ForceStopped = false;
				if (BallTrackAPI.ShotNameExisting(txtShotName.Text))
				{
					ShowLabel(LabelState.EqualName);
					txtShotName.SelectAll();
					txtShotName.Focus();
					return;
				}
				if (!IsSetupCompleted())
				{
					FrmMessageBox frmMessage = new FrmMessageBox();
					frmMessage.MessageMode = MessageStyle.SetupIncomplete;
					frmMessage.ShowDialog(this);
					return;
				}
				ShowTeacherControls(false);
				UpdateButtonState(TeachingState.Start);

				if (!BallTrackAPI.m_bStartTracking)
				{
					m_bShotFinished = false;
					m_bShotSaved = false;
					EnableTeachingControls(false);
					BallTrackAPI.StartTracking();
				}
			}
			else
			{
				int nShot = SearchShotByName(txtShotName.Text);
				if (nShot >= 0)
				{
					txtShotName.Text = BallTrackAPI.shotHistory.Shot[nShot].Name;
					txtShotDescription.Text = BallTrackAPI.shotHistory.Shot[nShot].Description;

					UpdateButtonState(TeachingState.SearchOK);
					cmbShotHistory.SelectedIndex = nShot;
				}
				else
					UpdateButtonState(TeachingState.SearchFail);

			}
		}
		//return index of shot stored in db
		private int SearchShotByName(string strShotName)
		{
			if (BallTrackAPI.shotHistory.Shot.Count <= 0)
				return -1;		//not found

			int nMatchingShot = -1;
			for (int i = 0; i < BallTrackAPI.shotHistory.Shot.Count; i++)
			{
				if (BallTrackAPI.shotHistory.Shot[i].Name.CompareTo(strShotName) == 0)
					return i;

				if (BallTrackAPI.shotHistory.Shot[i].Name.IndexOf(strShotName) >= 0)
					nMatchingShot = i;
			}

			return nMatchingShot;
		}
		private async void btnCancel_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();

			//Stop tracking
			//------//
			if (m_bUploadMode)
			{
				if (BallTrackAPI.m_bStartTracking)
				{
					BallTrackAPI.StopTracking();
					BallTrackAPI.ResetCurrentTrajectory();
					picBilliardTable.Invalidate();
					ForceStopped = true;
				}
				//
				ShowTeacherControls(true);
				EnableTeachingControls(true);
				if (m_bShotFinished && BallTrackAPI.m_nCurrentPlayer >= 0)
				{
					BallTrackAPI.BTAPI_DeleteShotRecord(BallTrackAPI.m_nCurrentPlayer, -1);
					UpdateButtonState(TeachingState.Cancel);
					m_bShotFinished = false;
					m_bShotSaved = false;
				}
				else
				{
					UpdateButtonState(TeachingState.Stop);
				}
			}
			else
			{
				FrmMessageBox frmMessage = new FrmMessageBox();
				frmMessage.MessageMode = MessageStyle.CancelShot;
				if (frmMessage.ShowDialog(this) == DialogResult.OK)
				{
					BallTrackAPI.DeleteShotHistory(cmbShotHistory.SelectedIndex);
					await BallTrackAPI.SaveShotHistory(lblTeacherName.Text);
					txtShotName.Clear();
					txtShotDescription.Clear();
					UpdateButtonState(TeachingState.Ready);
					cmbShotHistory.Text = "";
					UpdateShotList();
					m_bShotFinished = false;
				}
			}

		}
		private void EnableTeachingControls(bool bEnable)
		{
			int i;
			for (i = 0; i < InOutMarkCount; i++)
				picInOutMarks[i].Enabled = bEnable;

			for (i = 0; i < RailMarkCount; i++)
				txtRailMarks[i].Enabled = bEnable;

			txtRailCount.Enabled = bEnable;
		}
		private void UpdateButtonState(TeachingState state)
		{
			currentState = state;
			switch (state)
			{
				case TeachingState.Init:
					btnStart.Enabled = false;
					btnCancel.Enabled = false;
					btnReplay.Enabled = false;
					btnReset.Enabled = false;
					btnSave.Enabled = false;
					break;
				case TeachingState.Start:
					btnStart.Enabled = false;
					btnCancel.Enabled = true;
					btnReplay.Enabled = false;
					btnReset.Enabled = false;
					btnSave.Enabled = false;
					break;
				case TeachingState.Stop:
				case TeachingState.Save:
				case TeachingState.Ready:
				case TeachingState.Reset:
					btnStart.Enabled = true;
					btnCancel.Enabled = (!m_bUploadMode && cmbShotHistory.SelectedIndex >= 0);
					btnReplay.Enabled = (!m_bUploadMode && cmbShotHistory.SelectedIndex >= 0) || m_bShotFinished;
					btnReset.Enabled = true;
					btnSave.Enabled = false;
					break;
				case TeachingState.Cancel:		//save cancelled
					btnStart.Enabled = true;
					btnCancel.Enabled = false;
					btnReplay.Enabled = false;
					btnReset.Enabled = true;
					btnSave.Enabled = false;
					break;
				case TeachingState.Replay:
					btnStart.Enabled = false;
					btnCancel.Enabled = false;
					btnReplay.Enabled = false;
					btnReset.Enabled = false;
					btnSave.Enabled = false;
					break;

				case TeachingState.ShotFinish:
					if (m_bShotSaved)		//replay finished
					{
						btnStart.Enabled = true;
						btnCancel.Enabled = false;
						btnReplay.Enabled = true;
						btnReset.Enabled = false;
						btnSave.Enabled = false;
					}
					else
					{
						btnStart.Enabled = false;
						btnCancel.Enabled = true;
						btnReplay.Enabled = true;
						btnReset.Enabled = false;
						btnSave.Enabled = true;
					}
					break;
				case TeachingState.SearchOK:
					btnReplay.Enabled = true;
					break;
				case TeachingState.SearchFail:
					btnReplay.Enabled = false;
					break;
				default:
					break;
			}

			btnStart.Image = btnStart.Enabled ? BilliardWindowsApplication.Properties.Resources.start_button : BilliardWindowsApplication.Properties.Resources.start_disabled_button;
			btnCancel.Image = btnCancel.Enabled ? BilliardWindowsApplication.Properties.Resources.cancel_button : BilliardWindowsApplication.Properties.Resources.cancel_disabled_button;
			btnReplay.Image = btnReplay.Enabled ? BilliardWindowsApplication.Properties.Resources.replay_button : BilliardWindowsApplication.Properties.Resources.replay_disabled_button;
			btnReset.Image = btnReset.Enabled ? BilliardWindowsApplication.Properties.Resources.reset_button : BilliardWindowsApplication.Properties.Resources.reset_disabled_button;
			btnSave.Image = btnSave.Enabled ? BilliardWindowsApplication.Properties.Resources.save_button : BilliardWindowsApplication.Properties.Resources.save_disabled_button;
		}
		private void btnExit_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();

			if (BallTrackAPI.m_bStartTracking)
				BallTrackAPI.StopTracking();

			if (m_bShotFinished && m_bUploadMode)
			{
				FrmMessageBox frmMessage = new FrmMessageBox();
				frmMessage.MessageMode = MessageStyle.SaveShot;
				if (frmMessage.ShowDialog(this) == DialogResult.OK)
				{
					SaveShot();
				}
			}

			timerCounter.Enabled = false;
			costmail();

			if (m_bLoginOK)
				API.UpdatePlayerLogin(BLL_BilliardWindowsApplication.teacher.PlayerId, "0");

			API.UpdategameusbrelayAsync(BLL_BilliardWindowsApplication.gamecostdetailsStatic.id, "f");
			API.UpdategamecoststatusAsync(BLL_BilliardWindowsApplication.gamecostdetailsStatic.id, "OVER");
			BallTrackAPI.BTAPI_ShowTeacherPoint(false);

			//Process[] pname = Process.GetProcessesByName("osk");

			//if (pname != null && pname.Count() > 0) //modified by leo on 10/17 from || to &&
			//{
			//	pname[0].CloseMainWindow();
			//	pname[0].Close();
			//}

			this.Close();
		}
		private void SaveTeacherSetting()
		{
			for (int i = 0; i < RailMarkCount; i++)
			{
				Int32.TryParse(txtRailMarks[i].Text, out BallTrackAPI.m_nMarkValue[i]);
			}

			Int32.TryParse(txtRailCount.Text, out BallTrackAPI.m_nRailCount);

			BallTrackAPI.BTAPI_SetTeacherParam(ref BallTrackAPI.m_nMarkValue[0], BallTrackAPI.m_nInSide, BallTrackAPI.m_nOutSide, BallTrackAPI.m_nRailCount);
		}

		private void LoadTeacherSetting()
		{
			for (int i = 0; i < RailMarkCount; i++)
			{
				if (BallTrackAPI.m_nMarkValue[i] >= 0)
					txtRailMarks[i].Text = BallTrackAPI.m_nMarkValue[i].ToString();
				else
					txtRailMarks[i].Text = "";
			}

			txtRailCount.Text = BallTrackAPI.m_nRailCount.ToString();
			UpdateInOutMarkImage();
		}
		public void UpdateShotList()
		{
			if (!m_bLoginOK)
				return;

			cmbShotHistory.Items.Clear();

			foreach (ShotMetaInfo metaInfo in BallTrackAPI.shotHistory.Shot)
			{
				cmbShotHistory.Items.Add(metaInfo.Name);
			}
		}
		private void ResetAllInfo()
		{
			BallTrackAPI.InitTeacherSetting();
			LoadTeacherSetting();
			txtShotDescription.Text = strPromptShotDescription;
			txtShotName.Text = strPromptNewShotName;
			BallTrackAPI.ResetCurrentTrajectory();
			BallTrackAPI.ResetReplayTrajectory();
			picBilliardTable.Invalidate();
		}
		private void btnReset_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();
			ResetAllInfo();
			ShowTeacherControls(true);
		}
		private async void SaveShot()
		{
			ShotMetaInfo metaInfo = new ShotMetaInfo();
			metaInfo.Name = txtShotName.Text;
			if (string.IsNullOrEmpty(metaInfo.Name) || metaInfo.Name == strPromptNewShotName)
				metaInfo.Name = "Noname" + BallTrackAPI.shotHistory.Shot.Count.ToString("##");

			metaInfo.Description = txtShotDescription.Text;
			if (metaInfo.Description == "Description of shot")
				metaInfo.Description = "";

			metaInfo.nPlayer = BallTrackAPI.m_nCurrentPlayer;
			metaInfo.nShot = metaInfo.nPlayer == 0 ? BallTrackAPI.BTAPI_GetWhiteHistoryCount() : BallTrackAPI.BTAPI_GetYellowHistoryCount();
			metaInfo.nShot--;

			for (int i = 0; i < 28; i++)
			{
				metaInfo.lstRailMarkValue.Add(BallTrackAPI.m_nMarkValue[i]);
			}

			metaInfo.InSide = BallTrackAPI.m_nInSide;
			metaInfo.OutSide = BallTrackAPI.m_nOutSide;

			BallTrackAPI.shotHistory.Shot.Add(metaInfo);
			await BallTrackAPI.SaveShotHistory(lblTeacherName.Text);
		}
		private void btnSave_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();

			SaveShot();
			m_bShotFinished = false;
			UpdateButtonState(TeachingState.Save);
			ShowTeacherControls(true);
			m_bShotSaved = true;
			UpdateShotList();

			ResetAllInfo();

			txtShotName.Text = strPromptNewShotName;
			txtShotDescription.Text = strPromptShotDescription;
			txtRailCount.Text = "";
			BallTrackAPI.m_nInSide = -1;
			BallTrackAPI.m_nOutSide = -1;

			UpdateInOutMarkImage();
			picBallFeature.Invalidate();
		}

		private void btnReplay_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();
			if (m_bUploadMode && m_bShotFinished)
			{
				UpdateButtonState(TeachingState.Replay);
				int nShotCount = BallTrackAPI.m_nCurrentPlayer == 0 ? BallTrackAPI.BTAPI_GetWhiteHistoryCount() : BallTrackAPI.BTAPI_GetYellowHistoryCount();
				BallTrackAPI.m_bReplay = true;
				nReplayPlayer = BallTrackAPI.m_nCurrentPlayer;
				ShowTeacherControls(false);
				BallTrackAPI.BTAPI_DrawReplay(IntPtr.Zero, BallTrackAPI.m_nCurrentPlayer, nShotCount - 1);
			}
			else
			{
				int nShotNum = cmbShotHistory.SelectedIndex;
				if (nShotNum >= 0)
				{
					UpdateButtonState(TeachingState.Replay);
					ShowTeacherControls(false);
					ShotMetaInfo shot = BallTrackAPI.shotHistory.Shot[nShotNum];
					nReplayPlayer = shot.nPlayer;
					BallTrackAPI.m_bReplay = true;

					if (shot.lstRailMarkValue.Count == 28)
					{
						for (int i = 0; i < 28; i++)
							BallTrackAPI.m_nMarkValue[i] = shot.lstRailMarkValue[i];
					}
					BallTrackAPI.m_nInSide = shot.InSide;
					BallTrackAPI.m_nOutSide = shot.OutSide;

					LoadTeacherSetting();

					BallTrackAPI.BTAPI_DrawReplay(IntPtr.Zero, shot.nPlayer, shot.nShot);
				}
			}
			timerDraw.Enabled = true;
		}
		private void UpdateInOutMarkImage()
		{
			for (int i = 0; i < InOutMarkCount; i++)
			{
				if (i % 2 == 1)
				{
					if (BallTrackAPI.m_nOutSide * 2 + 1 == i)
						picInOutMarks[i].Image = BilliardWindowsApplication.Properties.Resources.green_circle;
					else
						picInOutMarks[i].Image = BilliardWindowsApplication.Properties.Resources.green_circle_boundary;
				}
				else
				{
					if (BallTrackAPI.m_nInSide * 2 == i)
						picInOutMarks[i].Image = BilliardWindowsApplication.Properties.Resources.red_circle;
					else
						picInOutMarks[i].Image = BilliardWindowsApplication.Properties.Resources.red_circle_boundary;
				}
			}
		}
		private void picInOut_Click(object sender, EventArgs e)
		{
			int nMarkNum = (int)((PictureBox)sender).Tag;

			if (nMarkNum >= 0)
			{
				if (nMarkNum % 2 == 1)
					BallTrackAPI.m_nOutSide = nMarkNum / 2;
				else
					BallTrackAPI.m_nInSide = nMarkNum / 2;

				UpdateInOutMarkImage();

				BLL_BilliardWindowsApplication.playclicksound();
			}
		}

		private void cmbShotHistory_KeyPress(object sender, KeyPressEventArgs e)
		{
			e.Handled = true;
		}
		private void FrmTeaching_Closing(object sender, EventArgs e)
		{

		}
		private void FrmTeaching_Load(object sender, EventArgs e)
		{
			Rectangle rcTableInside = m_rcDrawRect;
			rcTableInside.Inflate(-m_nMarginX, -m_nMarginY);
			m_fScaleX = (float)rcTableInside.Width / (float)(BallTrackAPI.g_rcClip.bottom - BallTrackAPI.g_rcClip.top);
			m_fScaleY = (float)rcTableInside.Height / (float)(BallTrackAPI.g_rcClip.right - BallTrackAPI.g_rcClip.left);

			if (BallTrackAPI.m_nInputMethod == 1)
			{
				BallTrackAPI.BTAPI_OpenVideoFile("8.avi");
			}
			else if (!BallTrackAPI.BTAPI_ConnectCamera(IntPtr.Zero))
                MessageBox.Show("Camera is not connected");

			m_rcFuncButtonUpload = new Rectangle[m_nFuncButton_Upload];
			m_rcFuncButtonDownload = new Rectangle[m_nFuncBUtton_Download];

			CalcFuncButtonLayout();

			ShowTeacherControls(true);
			UpdateModeButtonImages();
			SwitchUpDownMode(m_bUploadMode);
			BallTrackAPI.BTAPI_ShowTeacherPoint(true);

			System.Windows.Automation.AutomationElement.FromHandle(this.Handle);
		}

		private void CalcFuncButtonLayout()
		{
			PictureBox[] funcButtons = { btnStart, btnCancel, btnSave, btnReplay, btnReset, btnExit };

			//layout the position of function buttons
			int i;
			for (i = 0; i < funcButtons.Length; i++)
				m_rcFuncButtonUpload[i] = funcButtons[i].Bounds;

			int nMargin = btnSave.Location.Y - btnStart.Location.Y - btnStart.Height;
			int nLayoutHeight = btnStart.Height * 3 + nMargin * 2;
			int nDownloadTop = (btnStart.Location.Y + btnExit.Location.Y + btnExit.Height - nLayoutHeight) / 2;

			Rectangle rcDownloadButton = new Rectangle(btnStart.Location.X + 100, nDownloadTop, btnStart.Width, btnStart.Height);
			m_rcFuncButtonDownload[0] = rcDownloadButton;

			rcDownloadButton.Offset(0, btnStart.Height + nMargin);
			m_rcFuncButtonDownload[1] = rcDownloadButton;

			rcDownloadButton.Offset(0, btnStart.Height + nMargin);
			m_rcFuncButtonDownload[2] = rcDownloadButton;
		}
		private void timerDraw_Tick(object sender, EventArgs e)
		{
			try
			{
				BallTrackAPI.BTAPI_QueryDrawInfo(ref BallTrackAPI.drawInfo, ref BallTrackAPI.m_WhiteHitPos[0], ref BallTrackAPI.m_YellowHitPos[0], ref BallTrackAPI.m_RedHitPos[0], BallTrackAPI.m_bReplay, 0, 0);
				BallTrackAPI.ScaleCurrentDrawInfo(m_fScaleX, m_fScaleY, m_nMarginX, m_nMarginY, picBilliardTable.Width, picBilliardTable.Height);
				picBilliardTable.Invalidate();
			}
			catch { timerDraw.Enabled = false; }
		}
		public void DrawPlay(int nStep)
		{
			picBilliardTable.Invalidate();
		}
		private void picBilliardTable_Paint(object sender, PaintEventArgs e)
		{
			if (!BallTrackAPI.m_bReplay)
			{
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				
				if (BallTrackAPI.m_bStartTracking)
				{
					if (BallTrackAPI.m_whiteBallInfo.vptTrajectory.Count() > 0)
					{
						BallTrackAPI.DrawTrajectory(e.Graphics, BallTrackAPI.m_whiteBallInfo, BallTrackAPI.m_nCurrentStep, m_fScaleX, m_fScaleY, m_nMarginX, m_nMarginY, 0);
					}

					if (BallTrackAPI.m_yellowBallInfo.vptTrajectory.Count() > 0)
					{
						BallTrackAPI.DrawTrajectory(e.Graphics, BallTrackAPI.m_yellowBallInfo, BallTrackAPI.m_nCurrentStep, m_fScaleX, m_fScaleY, m_nMarginX, m_nMarginY, 1);
					}

					if (BallTrackAPI.m_redBallInfo.vptTrajectory.Count() > 0)
					{
						BallTrackAPI.DrawTrajectory(e.Graphics, BallTrackAPI.m_redBallInfo, BallTrackAPI.m_nCurrentStep, m_fScaleX, m_fScaleY, m_nMarginX, m_nMarginY, 2);
					}
				}
				if (txtRailCount.Visible)
				{
					using (Font labelFont = new Font("Microsoft Sans Serif", 13F, FontStyle.Bold))
					{
						string strLabel = "Please insert the number of rail";
						Size textSize = TextRenderer.MeasureText(strLabel, labelFont);
						textSize.Width = (int)(textSize.Width * 0.91f);
						PointF labelPos = new PointF();
						int nMargin = 6;
						labelPos.X = (float)((picBilliardTable.Width - textSize.Width) / 2);
						labelPos.Y = (float)(txtRailCount.Location.Y - picBilliardTable.Location.Y - textSize.Height * 3 - nMargin);
						SolidBrush brush = new SolidBrush(Color.Black);
						e.Graphics.DrawString(strLabel, labelFont, brush, labelPos);
						
						strLabel = "to impact";
						textSize = TextRenderer.MeasureText(strLabel, labelFont);
						textSize.Width = (int)(textSize.Width * 0.91f);
						labelPos.X = (float)((picBilliardTable.Width - textSize.Width) / 2);
						labelPos.Y = (float)(txtRailCount.Location.Y - picBilliardTable.Location.Y - textSize.Height * 2);
						e.Graphics.DrawString(strLabel, labelFont, brush, labelPos);

						strLabel = "Click over circle to fill it";
						textSize = TextRenderer.MeasureText(strLabel, labelFont);
						textSize.Width = (int)(textSize.Width * 0.91f);
						labelPos.X = (float)((picBilliardTable.Width - textSize.Width) / 2);
						labelPos.Y = (float)(txtRailCount.Bottom - picBilliardTable.Location.Y + textSize.Height);
						e.Graphics.DrawString(strLabel, labelFont, brush, labelPos);

						strLabel = "Red = first input";
						textSize = TextRenderer.MeasureText(strLabel, labelFont);
						textSize.Width = (int)(textSize.Width * 0.91f);
						labelPos.X = (float)((picBilliardTable.Width - textSize.Width) / 2);
						labelPos.Y = (float)(txtRailCount.Bottom - picBilliardTable.Location.Y + textSize.Height * 2);
						e.Graphics.DrawString(strLabel, labelFont, brush, labelPos);

						strLabel = "Green = last output";
						textSize = TextRenderer.MeasureText(strLabel, labelFont);
						textSize.Width = (int)(textSize.Width * 0.91f);
						labelPos.X = (float)((picBilliardTable.Width - textSize.Width) / 2);
						labelPos.Y = (float)(txtRailCount.Bottom - picBilliardTable.Location.Y + textSize.Height * 3);
						e.Graphics.DrawString(strLabel, labelFont, brush, labelPos);
					}
				}
			}
			else
			{
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				BallTrackAPI.DrawReplay(e.Graphics, BallTrackAPI.drawInfo.state, BallTrackAPI.drawInfo.player,
					BallTrackAPI.drawInfo.white_point, BallTrackAPI.drawInfo.yellow_point, BallTrackAPI.drawInfo.red_point,
					BallTrackAPI.drawInfo.start_point_white, BallTrackAPI.drawInfo.start_point_yellow, BallTrackAPI.drawInfo.start_point_red,
					BallTrackAPI.drawInfo.hit_count_white, BallTrackAPI.m_WhiteHitPos,
					BallTrackAPI.drawInfo.hit_count_yellow, BallTrackAPI.m_YellowHitPos,
					BallTrackAPI.drawInfo.hit_count_red, BallTrackAPI.m_RedHitPos,
					BallTrackAPI.m_bShowBallSpeed, BallTrackAPI.drawInfo.ball_speed, BallTrackAPI.drawInfo.impact_state,
					BallTrackAPI.drawInfo.teacher_mark_in, BallTrackAPI.drawInfo.teacher_mark_out,
					BallTrackAPI.drawInfo.teacher_point_in, BallTrackAPI.drawInfo.teacher_point_out);
			}
		}
		public void ShowLabel(LabelState state)
		{
			if (state == LabelState.WrongShotRed)
				pbLabel.Image = BilliardWindowsApplication.Properties.Resources.wrong_shot_red;
			else if (state == LabelState.EqualName)
				pbLabel.Image = BilliardWindowsApplication.Properties.Resources.equal_name;

			pbLabel.Location = new Point(660, 478);
			pbLabel.Visible = true;
		}
		public void StateProc(int nStep)
		{
			try
			{
				//add unnamed short temporarily
				//this.BeginInvoke(new Action(() => timerCounter.Enabled = false));
				System.Threading.Thread.Sleep(3000);
				BallTrackAPI.m_nCurrentStep = 2;
				BallTrackAPI.drawProc(2);
				m_bShotFinished = true;
				System.Threading.Thread.Sleep(3000);
				BallTrackAPI.StorePreviousTrajectory();
				BallTrackAPI.drawProc(BallTrackAPI.m_nCurrentStep);
				BallTrackAPI.StopTracking();
				if (!ForceStopped)
				{
					this.BeginInvoke(new Action(() => UpdateButtonState(TeachingState.ShotFinish)));
					this.BeginInvoke(new Action(() => EnableTeachingControls(true)));

					if (!BallTrackAPI.m_bFirstInputOK)
					{
						this.BeginInvoke(new Action(() => ShowLabel(LabelState.WrongShotRed)));
					}
					else if (!BallTrackAPI.m_bLastOutputOK)
					{

					}
				}

				ForceStopped = false;
				//System.Threading.Thread.Sleep(1500);
				//this.BeginInvoke(new Action(() => ShowTeacherControls(true)));
			}
			catch (Exception)
			{

			}
		}
		private void cmbShotHistory_SelectedIndexChanged(object sender, EventArgs e)
		{
			int nShotNum = cmbShotHistory.SelectedIndex;
			if (nShotNum < 0)
				return;

			UpdateButtonState(TeachingState.Ready);
			txtShotName.Text = BallTrackAPI.shotHistory.Shot[nShotNum].Name;
			string[] description = BallTrackAPI.shotHistory.Shot[nShotNum].Description.Split('\n');
			txtShotDescription.Text = "";
			foreach (var line in description)
			{
				txtShotDescription.Text += line;
				txtShotDescription.Text += Environment.NewLine;
			}
		}

		private void txtPw_KeyPress(object sender, KeyPressEventArgs e)
		{
			
		}
		private async Task<string> GetPlayerNameFromPW(string pw)
		{
			//if (BallTrackAPI.m_nInputMethod == 1)
			//	return "Test123456";
			//should get playerdetail from db by pw
			//update player image and club image
			//return name of player(teacher), this will be used as a key to save/load shot history from db or local file for that player

			try
			{
				BLL_BilliardWindowsApplication.teacher = await Task.Run(() => API.getPlayerDetails(pw));
			}
			catch(Exception e)
			{
				MessageBox.Show(e.Message);
				return "Test123456";
			}

			return BLL_BilliardWindowsApplication.teacher.Name;
		}
		async void insertgamecost(biliardService.gamecostdetails gcdetails)
		{
			await Task.Run(() => API.Updategamecost(gcdetails));
			//MessageBox.Show(respond);
		}
		private async void txtPw_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				if (m_bLoginOK)
				{
					MessageBox.Show("Please exit current game to login as another teacher.");
					return;
				}
				string strPlayerName = await GetPlayerNameFromPW(txtPw.Text);
				if (string.IsNullOrEmpty(strPlayerName))
				{
					if (m_bLoginOK)
					{
						API.UpdatePlayerLogin(BLL_BilliardWindowsApplication.teacher.PlayerId, "0");
						lblTeacherName.Text = "";
						picTeacher.ImageLocation = "";
						picClub.ImageLocation = "";
						timerCounter.Enabled = false;
					}
					MessageBox.Show("Password is incorrect!");
					m_bLoginOK = false;
					return;
				}
				else
				{
					if (!string.IsNullOrEmpty(BLL_BilliardWindowsApplication.teacher.Name))
					{
						lblTeacherName.Text = BLL_BilliardWindowsApplication.teacher.Name + " " + BLL_BilliardWindowsApplication.teacher.FamilyName;
						picTeacher.ImageLocation = BLL_BilliardWindowsApplication.originalLocation + BLL_BilliardWindowsApplication.teacher.PlayerPicture;
						picClub.ImageLocation = BLL_BilliardWindowsApplication.originalLocation + BLL_BilliardWindowsApplication.teacher.ClubPicture;


						if (BilliardWindowsApplication.Properties.Settings.Default.billiardno > 0)
						{
							string cost = currentCost.Replace("€", "").Replace(",", ".");
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.billno = BilliardWindowsApplication.Properties.Settings.Default.billiardno.ToString();
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.clubid = BilliardWindowsApplication.Properties.Settings.Default.CID;
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.date = DateTime.Now.Date.Day + "/" + DateTime.Now.Date.Month + "/" + DateTime.Now.Date.Year;
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.duration = (starttime.Hours * 60) / 10 + (starttime.Hours * 60) % 10 + starttime.Minutes / 10 + starttime.Minutes % 10 + "";
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.fromtime = currentTime.Hours / 10 + "" + currentTime.Hours % 10 + ":" + currentTime.Minutes / 10 + "" + currentTime.Minutes % 10 + "";

							BLL_BilliardWindowsApplication.gamecostdetailsStatic.p1 = BLL_BilliardWindowsApplication.teacher.PlayerId;
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.p2 = "0";
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.p3 = "0";
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.p4 = "0";

							if (BLL_BilliardWindowsApplication.costDetailsStataic.coston == "t")
							{
								BLL_BilliardWindowsApplication.gamecostdetailsStatic.totcost = cost.Replace("€", "").Replace(",", ".");

								BLL_BilliardWindowsApplication.gamecostdetailsStatic.costplayer = (double.Parse(currentCost.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 1).ToString().Replace(",", ".");
							}
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.totime = DateTime.Now.Hour / 10 + DateTime.Now.Hour % 10 + ":" + DateTime.Now.Minute / 10 + DateTime.Now.Minute % 10 + "";
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.noplayers = "1";
							BLL_BilliardWindowsApplication.gamecostdetailsStatic.gameover = false.ToString();
							insertgamecost(BLL_BilliardWindowsApplication.gamecostdetailsStatic);
						}

						API.UpdatePlayerLogin(BLL_BilliardWindowsApplication.teacher.PlayerId, "1");
					}
					m_bLoginOK = true;
					UpdateButtonState(TeachingState.Ready);
					EnableTeachingControls(m_bUploadMode);
					timerCounter.Enabled = true;
					await BallTrackAPI.LoadShotHistory(strPlayerName);
					UpdateShotList();

					e.Handled = true;

					txtPw.Enabled = false;
				}
			}
		}

		private async void NotifyProc(int nNotifyCode, int nValue)
		{
			if (nNotifyCode == 4) //replay finished
			{
				timerDraw.Enabled = false;
				picBilliardTable.Invalidate();
				picBilliardTable.BeginInvoke(new Action(() => picBilliardTable.Update()));
				await Task.Delay(100);
				if (m_bUploadMode)
					this.BeginInvoke(new Action(() => UpdateButtonState(TeachingState.ShotFinish)));
				else
					this.BeginInvoke(new Action(() => UpdateButtonState(TeachingState.Ready)));

				BallTrackAPI.m_bReplay = false;
				//BallTrackAPI.ResetReplayTrajectory();
			}
		}

		private void txtShotName_Enter(object sender, EventArgs e)
		{
			if (txtShotName.Text == strPromptNewShotName || txtShotName.Text == strPromptShotNameForSearch)
				txtShotName.Text = "";
		}

		private void txtShotName_Leave(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(txtShotName.Text))
			{
				if (m_bUploadMode)
					txtShotName.Text = strPromptNewShotName;
				else
					txtShotName.Text = strPromptShotNameForSearch;
			}
		}

		private void txtShotDescription_Enter(object sender, EventArgs e)
		{
			if (txtShotDescription.Text == strPromptShotDescription)
				txtShotDescription.Text = "";
		}

		private void txtShotDescription_Leave(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(txtShotDescription.Text))
				txtShotDescription.Text = strPromptShotDescription;
		}

		private void UpdateModeButtonImages()
		{
			picUpload.Image = m_bUploadMode ? BilliardWindowsApplication.Properties.Resources.white_green_circle : BilliardWindowsApplication.Properties.Resources.circle;
			picDownload.Image = m_bUploadMode ? BilliardWindowsApplication.Properties.Resources.circle : BilliardWindowsApplication.Properties.Resources.white_red_circle;
		}
		private void picUpload_Click(object sender, EventArgs e)
		{
            if (m_bUploadMode || !m_bLoginOK || BallTrackAPI.m_bReplay || BallTrackAPI.m_bStartTracking)
				return;
			BLL_BilliardWindowsApplication.playclicksound();
			m_bUploadMode = true;
			UpdateModeButtonImages();
			SwitchUpDownMode(true);
		}

		private void picDownload_Click(object sender, EventArgs e)
		{
            if (!m_bUploadMode || !m_bLoginOK || BallTrackAPI.m_bReplay || BallTrackAPI.m_bStartTracking)
				return;
			BLL_BilliardWindowsApplication.playclicksound();
			m_bUploadMode = false;
			UpdateModeButtonImages();
			SwitchUpDownMode(false);
		}

		private void lblUpload_Click(object sender, EventArgs e)
		{
			if (m_bUploadMode || !m_bLoginOK || BallTrackAPI.m_bReplay || BallTrackAPI.m_bStartTracking)
				return;

			BLL_BilliardWindowsApplication.playclicksound();
			m_bUploadMode = true;
			UpdateModeButtonImages();
			SwitchUpDownMode(true);
		}

		private void lblDownload_Click(object sender, EventArgs e)
		{
            if (!m_bUploadMode || !m_bLoginOK || BallTrackAPI.m_bReplay || BallTrackAPI.m_bStartTracking)
				return;

			BLL_BilliardWindowsApplication.playclicksound();
			m_bUploadMode = false;
			UpdateModeButtonImages();
			SwitchUpDownMode(false);
		}

		private void lblBallFeature_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();
			m_bBallFeatureChecked = !m_bBallFeatureChecked;
			picBallFeature.Image = m_bBallFeatureChecked ? BilliardWindowsApplication.Properties.Resources.white_green_circle : BilliardWindowsApplication.Properties.Resources.circle;
            if (m_bUploadMode)
                BallTrackAPI.ShowBallSpeed(m_bBallFeatureChecked);
		}

		private void SwitchUpDownMode(bool bUploadMode)
		{
			if (!m_bLoginOK && !m_bFirstRun)
			{
				return;
			}

			m_bFirstRun = false;
			int i;
			if (bUploadMode)
			{
				//lblCost.Visible = true;
				PictureBox[] funcButtonsUpload = { btnStart, btnCancel, btnReplay, btnSave, btnReset, btnExit };
				for (i = 0; i < funcButtonsUpload.Length; i++)
				{
					funcButtonsUpload[i].Bounds = m_rcFuncButtonUpload[i];
				}

				txtShotName.Text = strPromptNewShotName;
				for (i = 0; i < funcButtonsUpload.Length; i++)
					funcButtonsUpload[i].Visible = true;

				//if (BallTrackAPI.shotHistory.Shot.Count > 0)
				//{
				//	ShotMetaInfo lastShotInfo = BallTrackAPI.shotHistory.Shot[BallTrackAPI.shotHistory.Shot.Count - 1];
				//	BallTrackAPI.BTAPI_DeleteShotRecord(lastShotInfo.nPlayer, -1);
				//	BallTrackAPI.shotHistory.Shot.RemoveAt(BallTrackAPI.shotHistory.Shot.Count - 1);
				//	BallTrackAPI.SaveShotHistory(lblTeacherName.Text);
				//}

				ResetAllInfo();

                BallTrackAPI.ShowBallSpeed(m_bBallFeatureChecked);
			}
			else
			{
				if (m_bShotFinished)
				{
					FrmMessageBox frmMessage = new FrmMessageBox();
					frmMessage.MessageMode = MessageStyle.SaveShot;
					if (frmMessage.ShowDialog(this) == DialogResult.OK)
					{
						SaveShot();
					}
					m_bShotFinished = false;
				}
				//lblCost.Visible = false;
				//timerCounter.Enabled = false;
				PictureBox[] funcButtonsDownload = {btnReplay, btnCancel, btnExit };
				for (i = 0; i < funcButtonsDownload.Length; i++)
					funcButtonsDownload[i].Bounds = m_rcFuncButtonDownload[i];

				btnStart.Visible = false;
				btnSave.Visible = false;
				btnReset.Visible = false;

				txtShotName.Text = strPromptShotNameForSearch;
				UpdateShotList();
				cmbShotHistory.Text = "";
                BallTrackAPI.ShowBallSpeed(true);
			}

			ResetAllInfo();
			ShowTeacherControls(m_bUploadMode);
			EnableTeachingControls(m_bUploadMode);
			cmbShotHistory.Visible = !m_bUploadMode;
			txtShotDescription.Text = m_bUploadMode ? strPromptShotDescription : "";
			txtShotName.Text = m_bUploadMode ? strPromptNewShotName : "";

			txtShotDescription.Enabled = m_bUploadMode;
			txtShotName.Enabled = m_bUploadMode;

			if (m_bUploadMode)
			{
				//panelKeyPad.Visible = true;
				//try
				//{
				//	Process[] pname = Process.GetProcessesByName("osk");

				//	Process oskProc = null;
				//	if (pname == null || pname.Count() == 0)
				//	{
				//		oskProc = System.Diagnostics.Process.Start("osk.exe");
				//	}
				//	else
				//		oskProc = pname[0];

				//	if (oskProc != null)
				//	{
				//		USER32Dll.ShowWindow(oskProc.MainWindowHandle, (int)ShowWindowCommands.SW_SHOWNORMAL);

				//		USER32Dll.MoveWindow(oskProc.MainWindowHandle, txtShotDescription.Left - 8, txtShotDescription.Bottom + 20,
				//			btnExit.Right - txtShotDescription.Left + 8, picInOutMarks[5].Bottom - txtShotDescription.Bottom - 45);
				//	}
				//}
				//catch(Exception)
				//{

				//}
			}
			else
			{
				//panelKeyPad.Visible = false;
				//try
				//{
				//	System.Diagnostics.Process[] pname = System.Diagnostics.Process.GetProcessesByName("osk");

				//	System.Diagnostics.Process oskProc = null;
				//	if (pname == null || pname.Count() == 0)
				//	{
				//		ProcessStartInfo startInfo = new ProcessStartInfo("osk.exe");
				//		startInfo.WindowStyle = ProcessWindowStyle.Minimized;
				//		oskProc = Process.Start(startInfo);
				//	}
				//	else
				//		oskProc = pname[0];

				//	if (oskProc != null)
				//	{
				//		USER32Dll.ShowWindow(oskProc.MainWindowHandle, (int)ShowWindowCommands.SW_SHOWMINIMIZED);
				//	}
				//}
				//catch (Exception)
				//{

				//}
			}

			UpdateButtonState(TeachingState.Ready);
		}

		void costmail()
		{
			if (string.IsNullOrEmpty(BLL_BilliardWindowsApplication.teacher.Name))
				return;
			try
			{

				if (usbrelay.relay1on == 1)
				{
					int a = usbrelay.usb_relay_device_close_one_relay_channel(usbrelay.hHandle, 01);
					if (a == 0) usbrelay.relay1on = 0;
					//MessageBox.Show("" + a);
				}


				if (usbrelay.relay2on == 1)
				{
					int a = usbrelay.usb_relay_device_close_one_relay_channel(usbrelay.hHandle, 02);
					if (a == 0) usbrelay.relay2on = 0;
					//MessageBox.Show("" + a);
				}

				if (BilliardWindowsApplication.Properties.Settings.Default.billiardno > 0)
				{

					string cost = currentCost.Replace("€", "").Replace(",", ".");

					BLL_BilliardWindowsApplication.gamecostdetailsStatic.billno = BilliardWindowsApplication.Properties.Settings.Default.billiardno.ToString();
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.clubid = BilliardWindowsApplication.Properties.Settings.Default.CID;
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.date = DateTime.Now.Date.Day + "/" + DateTime.Now.Date.Month + "/" + DateTime.Now.Date.Year;
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.duration = (starttime.Hours * 60) / 10 + (starttime.Hours * 60) % 10 + starttime.Minutes / 10 + starttime.Minutes % 10 + "";
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.fromtime = currentTime.Hours / 10 + "" + currentTime.Hours % 10 + ":" + currentTime.Minutes / 10 + "" + currentTime.Minutes % 10 + "";

					BLL_BilliardWindowsApplication.gamecostdetailsStatic.p1 = BLL_BilliardWindowsApplication.teacher.PlayerId;
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.p2 = "0";
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.p3 = "0";
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.p4 = "0";

					if (BLL_BilliardWindowsApplication.costDetailsStataic.coston == "t")
					{
						BLL_BilliardWindowsApplication.gamecostdetailsStatic.totcost = cost.Replace("€", "").Replace(",", ".");

						BLL_BilliardWindowsApplication.gamecostdetailsStatic.costplayer = (double.Parse(cost, CultureInfo.InvariantCulture) / 1).ToString().Replace(",", ".");
					}
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.totime = DateTime.Now.Hour / 10 + "" + DateTime.Now.Hour % 10 + ":" + DateTime.Now.Minute / 10 + "" + DateTime.Now.Minute % 10 + "";
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.noplayers = "1";
					BLL_BilliardWindowsApplication.gamecostdetailsStatic.gameover = true.ToString();
					insertgamecost(BLL_BilliardWindowsApplication.gamecostdetailsStatic);

					BLL_BilliardWindowsApplication.costDetailsStataic.d1 = int.Parse(BLL_BilliardWindowsApplication.costDetailsStataic.d1) + slottime1 + "";
					BLL_BilliardWindowsApplication.costDetailsStataic.d2 = int.Parse(BLL_BilliardWindowsApplication.costDetailsStataic.d2) + slottime2 + "";
					BLL_BilliardWindowsApplication.costDetailsStataic.d3 = int.Parse(BLL_BilliardWindowsApplication.costDetailsStataic.d3) + slottime3 + "";
					BLL_BilliardWindowsApplication.costDetailsStataic.d4 = int.Parse(BLL_BilliardWindowsApplication.costDetailsStataic.d4) + slottime4 + "";
					API.updatecostdaysAsync(BLL_BilliardWindowsApplication.costDetailsStataic);

					// cost of game mail
					string gamecostclub = "<img src = " + '"' + BLL_BilliardWindowsApplication.originalLocation + "img/logoTop.png" + '"' + " /><br /><br />" +
					" <div style =" + '"' + "border-top:3px solid #22BCE5; border-top-width: 1px;" + '"' + "></div>" +
					"<span style = " + '"' + "font-family:Arial;font-size:10pt" + '"' + ">      <h1>  <strong>Cost of Billiard</strong><br /></h1>";

					if (!string.IsNullOrEmpty(BLL_BilliardWindowsApplication.teacher.Name))
					{
						string gamecostbody = htmlgamecostcode(BLL_BilliardWindowsApplication.teacher.Name + " " + BLL_BilliardWindowsApplication.teacher.FamilyName, BLL_BilliardWindowsApplication.gamecostdetailsStatic.costplayer.Replace(".", ","));
						if (BLL_BilliardWindowsApplication.costDetailsStataic.emailcostPlayer == "t")
							SendHtmlFormattedEmail(BLL_BilliardWindowsApplication.teacher.PlayerId, "Cost of Billiard", gamecostbody);

						if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
							gamecostclub += "<br><b>TOTAL TO BE CASHED : Cost+Special " + BLL_BilliardWindowsApplication.gamecostdetailsStatic.totcost.Replace(".", ",") + " Euro</b><br><br>-----<br><b>" + BLL_BilliardWindowsApplication.teacher.Name + " " + BLL_BilliardWindowsApplication.teacher.FamilyName + "</b><br /><br />";
						else gamecostclub += "<br><b>TOTAL TO BE CASHED : Cost " + BLL_BilliardWindowsApplication.gamecostdetailsStatic.totcost.Replace(".", ",") + " Euro</b><br><br>-----<br><b>" + BLL_BilliardWindowsApplication.teacher.Name + " " + BLL_BilliardWindowsApplication.teacher.FamilyName + "</b><br /><br />";
						gamecostclub += "Billiard Number : " + BLL_BilliardWindowsApplication.costDetailsStataic.bilino + "<br>" +
						"Date : " + DateTime.Now.Date.ToShortDateString() + "  <br>";
						if (currentTime.Hours > 12)
							gamecostbody = gamecostbody + "From " + (currentTime.Hours - 12) / 10 + (currentTime.Hours - 12) % 10 + ":" + currentTime.Minutes / 10 + currentTime.Minutes % 10 + "PM";
						else if (currentTime.Hours == 12)
							gamecostbody = gamecostbody + "From " + currentTime.Hours / 10 + currentTime.Hours % 10 + ":" + currentTime.Minutes / 10 + currentTime.Minutes % 10 + "PM";
						else gamecostbody = gamecostbody + "From " + currentTime.Hours / 10 + currentTime.Hours % 10 + ":" + currentTime.Minutes / 10 + currentTime.Minutes % 10 + "AM";
						if (DateTime.Now.Hour > 12)
							gamecostbody = gamecostbody + " / To " + (DateTime.Now.Hour - 12) / 10 + (DateTime.Now.Hour - 12) % 10 + ":" + DateTime.Now.Minute / 10 + DateTime.Now.Minute % 10 + "PM  <br>";
						else if (DateTime.Now.Hour == 12)
							gamecostbody = gamecostbody + " / To " + DateTime.Now.Hour / 10 + DateTime.Now.Hour % 10 + ":" + DateTime.Now.Minute / 10 + DateTime.Now.Minute % 10 + "PM  <br>";
						else gamecostbody = gamecostbody + " / To " + DateTime.Now.Hour / 10 + DateTime.Now.Hour % 10 + ":" + DateTime.Now.Minute / 10 + DateTime.Now.Minute % 10 + "AM  <br>";
						if (slottime1 > 0)
							gamecostclub = gamecostclub + "Slot Time n<sup>o</sup> " + "1" + " / n<sup>o</sup> " + slottime1 + " minutes <br>";
						if (slottime2 > 0)
							gamecostclub = gamecostclub + "Slot Time n<sup>o</sup> " + "2" + " / n<sup>o</sup> " + slottime2 + " minutes <br>";
						if (slottime3 > 0)
							gamecostclub = gamecostclub + "Slot Time n<sup>o</sup> " + "3" + " / n<sup>o</sup> " + slottime3 + " minutes <br>";
						if (slottime4 > 0)
							gamecostclub = gamecostclub + "Slot Time n<sup>o</sup> " + "4" + " / n<sup>o</sup> " + slottime4 + " minutes <br>";

						gamecostclub = gamecostclub +

					"Total time n<sup>o</sup> " + (starttime.Hours * 60 + starttime.Minutes) + " Minutes <br>";
						if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
							gamecostclub = gamecostclub + "Total cost to be payed : <b> Cost+Special " + BLL_BilliardWindowsApplication.gamecostdetailsStatic.costplayer.Replace(".", ",") + " Euro </b><br><br />";
						else gamecostclub = gamecostclub + "Total cost to be payed : <b> Cost " + BLL_BilliardWindowsApplication.gamecostdetailsStatic.costplayer.Replace(".", ",") + " Euro </b><br><br />";


					}
					
					gamecostclub += "Thanks<br />" +
					"Biliardo Professionale<br>" +
					"<b>" + "Club " + BilliardWindowsApplication.Properties.Settings.Default.clubname + "</b>" + "</span>";

					if (BLL_BilliardWindowsApplication.costDetailsStataic.emailcostowner == "t")
						SendHtmlFormattedEmail(BilliardWindowsApplication.Properties.Settings.Default.emailid, "Cost of Billiard", gamecostclub);
				}
			}
			catch (Exception ex) { MessageBox.Show(ex.ToString()); }


		}
		private async void SendHtmlFormattedEmail(string recepientEmail, string subject, string body)
		{
			if (BallTrackAPI.m_nInputMethod == 1)
				return;

			try
			{
				await Task.Run(() => API.getmail("", "info@biliardoprofessionale.it", recepientEmail, subject, body));
			}
			catch { }
		}
		string htmlgamecostcode(string playername, string costplayer)
		{
			string gamecostbody = "<img src = " + '"' + BLL_BilliardWindowsApplication.originalLocation + "img/logoTop.png" + '"' + " /><br /><br />" +
				" <div style =" + '"' + "border-top:3px solid #22BCE5; border-top-width: 1px;" + '"' + "></div>" +
				"<span style = " + '"' + "font-family:Arial;font-size:10pt" + '"' + ">      <h1>  <strong>Cost of Billiard</strong><br /></h1>" +
				"Hello <b>" + playername + "</b><br /><br />" +
				"Billiard Number : " + BLL_BilliardWindowsApplication.costDetailsStataic.bilino + "<br>" +
				"Date : " + DateTime.Now.Date.ToShortDateString() + "  <br>";
			if (currentTime.Hours > 12)
				gamecostbody = gamecostbody + "From " + (currentTime.Hours - 12) / 10 + (currentTime.Hours - 12) % 10 + ":" + currentTime.Minutes / 10 + currentTime.Minutes % 10 + "PM";
			else if (currentTime.Hours == 12)
				gamecostbody = gamecostbody + "From " + currentTime.Hours / 10 + currentTime.Hours % 10 + ":" + currentTime.Minutes / 10 + currentTime.Minutes % 10 + "PM";
			else gamecostbody = gamecostbody + "From " + currentTime.Hours / 10 + currentTime.Hours % 10 + ":" + currentTime.Minutes / 10 + currentTime.Minutes % 10 + "AM";
			if (DateTime.Now.Hour > 12)
				gamecostbody = gamecostbody + " / To " + (DateTime.Now.Hour - 12) / 10 + (DateTime.Now.Hour - 12) % 10 + ":" + DateTime.Now.Minute / 10 + DateTime.Now.Minute % 10 + "PM  <br>";
			else if (DateTime.Now.Hour == 12)
				gamecostbody = gamecostbody + " / To " + DateTime.Now.Hour / 10 + DateTime.Now.Hour % 10 + ":" + DateTime.Now.Minute / 10 + DateTime.Now.Minute % 10 + "PM  <br>";
			else gamecostbody = gamecostbody + " / To " + DateTime.Now.Hour / 10 + DateTime.Now.Hour % 10 + ":" + DateTime.Now.Minute / 10 + DateTime.Now.Minute % 10 + "AM  <br>";
			if (slottime1 > 0)
				gamecostbody = gamecostbody + "Slot Time n<sup>o</sup> " + "1" + " / n<sup>o</sup> " + slottime1 + " minutes <br>";
			if (slottime2 > 0)
				gamecostbody = gamecostbody + "Slot Time n<sup>o</sup> " + "2" + " / n<sup>o</sup> " + slottime2 + " minutes <br>";
			if (slottime3 > 0)
				gamecostbody = gamecostbody + "Slot Time n<sup>o</sup> " + "3" + " / n<sup>o</sup> " + slottime3 + " minutes <br>";
			if (slottime4 > 0)
				gamecostbody = gamecostbody + "Slot Time n<sup>o</sup> " + "4" + " / n<sup>o</sup> " + slottime4 + " minutes <br>";
			gamecostbody = gamecostbody +
				"Total time n<sup>o</sup> " + (starttime.Hours * 60 + starttime.Minutes) + " Minutes <br>";


			if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
				gamecostbody = gamecostbody + "Total cost to be payed : <b> Cost+Special " + costplayer + " Euro </b><br><br />";
			else gamecostbody = gamecostbody + "Total cost to be payed : <b> Cost " + costplayer + " Euro </b><br><br />";
			gamecostbody = gamecostbody + "Thanks<br />" +
			   "Biliardo Professionale<br>" +
			   "<b>" + "Club " + BilliardWindowsApplication.Properties.Settings.Default.clubname + "</b>" + "</span>";
			return gamecostbody;
		}

		private void pbLabel_Click(object sender, EventArgs e)
		{
			BLL_BilliardWindowsApplication.playclicksound();
			pbLabel.Visible = false;
		}

		public void btnKeypad_Click(object sender, EventArgs e)
		{
			string tag = ((PictureBox)sender).Tag.ToString();
			if (tag == "Shift")
			{
				shiftKeyState++;
				shiftKeyState = shiftKeyState % 3;

				if (shiftKeyState == 0)
				{
					pbLShift.Image = BilliardWindowsApplication.Properties.Resources.shift;
					pbRShift.Image = BilliardWindowsApplication.Properties.Resources.shift;
				}
				else if (shiftKeyState == 1)
					((PictureBox)sender).Image = BilliardWindowsApplication.Properties.Resources.shift_hl;
				else if (shiftKeyState == 2)
				{
					pbLShift.Image = BilliardWindowsApplication.Properties.Resources.shift_hl;
					pbRShift.Image = BilliardWindowsApplication.Properties.Resources.shift_hl;
				}
			}
			else
			{
 				string key = ((PictureBox)sender).Tag.ToString();
				//if (shiftKeyState == 0)
				//	key = key.ToLower();
				//else if (shiftKeyState == 1)
				//{
				//	key = key.ToUpper();
				//	shiftKeyState = 0;
				//	pbLShift.Image = BilliardWindowsApplication.Properties.Resources.shift;
				//	pbRShift.Image = BilliardWindowsApplication.Properties.Resources.shift;
				//}
				//else if (shiftKeyState == 2)
					key = key.ToUpper();

				SendKeys.Send(key);
			}
		}

		private void timerCounter_Tick(object sender, EventArgs e)
		{
			try
			{
				int t1 = starttime.Minutes;
				starttime = starttime.Add(new TimeSpan(0, 0, 1));
				int t2 = starttime.Minutes;
				try
				{
					if (t1 != t2)
						calculatecostlbl();
				}
				catch (Exception ex) { MessageBox.Show("ex costcalutate:- " + ex.ToString()); }

				if ((BilliardWindowsApplication.Properties.Settings.Default.billiardno > 0 && int.Parse(BilliardWindowsApplication.Properties.Settings.Default.CID) > 0) && (BLL_BilliardWindowsApplication.costDetailsStataic.coston == "t" && BLL_BilliardWindowsApplication.costDetailsStataic.costvisible == "t"))
					lblCost.Text = starttime.ToString() + " - " + currentCost;
				else lblCost.Text = starttime.ToString();
			}
			catch (Exception ex) { MessageBox.Show("exno tgs3" + ex.ToString()); }
		}

		void calculatecostlbl()
		{
			TimeSpan t1, t2, t;

			int hrs = DateTime.Now.Hour;
			int min = DateTime.Now.Minute;

			t = new TimeSpan(hrs, min, 0);
			t1 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.f1);
			t2 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.t1);
			if (t1 < t2)
			{
				if (t >= t1 && t <= t2)
				{
					slottime1++;

					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h1.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;

				}
			}
			else
			{
				if (t >= t1 || t <= t2)
				{
					slottime1++;
					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h1.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;
				}
			}

			t1 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.f2);
			t2 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.t2);
			if (t1 < t2)
			{
				if (t >= t1 && t <= t2)
				{
					slottime2++;
					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h2.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;
				}
			}
			else
			{
				if (t >= t1 || t <= t2)
				{
					slottime2++;
					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h2.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;
				}
			}


			t1 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.f3);
			t2 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.t3);
			if (t1 < t2)
			{
				if (t >= t1 && t <= t2)
				{
					slottime3++;
					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h3.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;
				}
			}
			else
			{
				if (t >= t1 || t <= t2)
				{
					slottime3++;
					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h3.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;
				}
			}


			t1 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.f4);
			t2 = bllBillardLogic.convertDbToTime(BLL_BilliardWindowsApplication.costDetailsStataic.t4);
			if (t1 < t2)
			{
				if (t >= t1 && t <= t2)
				{
					slottime4++;
					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h4.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;
				}
			}
			else
			{
				if (t >= t1 || t <= t2)
				{
					slottime4++;
					double newcost = Convert.ToDouble(BLL_BilliardWindowsApplication.costDetailsStataic.h4.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 60;
					if (BLL_BilliardWindowsApplication.costDetailsStataic.SpecialBool == "t")
						dcost = dcost + (newcost * Convert.ToInt32(BLL_BilliardWindowsApplication.costDetailsStataic.SpecialCharge)) / 100 + newcost;
					else dcost = dcost + newcost;

				}
			}

			currentCost = dcost.ToString("0.00€");

			if (BilliardWindowsApplication.Properties.Settings.Default.billiardno > 0 && BLL_BilliardWindowsApplication.costDetailsStataic.coston == "t")
			{
				string cost = currentCost.Replace("€", "").Replace(",", ".");
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.billno = BilliardWindowsApplication.Properties.Settings.Default.billiardno.ToString();
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.clubid = BilliardWindowsApplication.Properties.Settings.Default.CID;
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.date = DateTime.Now.Date.Day + "/" + DateTime.Now.Date.Month + "/" + DateTime.Now.Date.Year;
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.duration = (slottime1 + slottime2 + slottime3 + slottime4).ToString();
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.fromtime = currentTime.Hours / 10 + "" + currentTime.Hours % 10 + ":" + currentTime.Minutes / 10 + "" + currentTime.Minutes % 10 + "";

				BLL_BilliardWindowsApplication.gamecostdetailsStatic.p1 = BLL_BilliardWindowsApplication.teacher.PlayerId;
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.p2 = "0";
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.p3 = "0";
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.p4 = "0";

				BLL_BilliardWindowsApplication.gamecostdetailsStatic.totcost = cost.Replace("€", "").Replace(",", ".");

				BLL_BilliardWindowsApplication.gamecostdetailsStatic.costplayer = (double.Parse(currentCost.Replace("€", "").Replace(",", "."), CultureInfo.InvariantCulture) / 1).ToString().Replace(",", ".");

				BLL_BilliardWindowsApplication.gamecostdetailsStatic.totime = DateTime.Now.Hour / 10 + "" + DateTime.Now.Hour % 10 + ":" + DateTime.Now.Minute / 10 + "" + DateTime.Now.Minute % 10 + "";
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.noplayers = "1";
				BLL_BilliardWindowsApplication.gamecostdetailsStatic.gameover = false.ToString();
				insertgamecost(BLL_BilliardWindowsApplication.gamecostdetailsStatic);
			}
		}
    }
}
	