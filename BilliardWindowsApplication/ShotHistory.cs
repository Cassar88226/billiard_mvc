﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;

namespace BilliardWindowsApplication
{
	[XmlRoot("ShotHistory")]
	public class ShotHistory
	{
		public List<ShotMetaInfo> Shot = new List<ShotMetaInfo>();
		public static bool LoadFromFile(string strFilePath, ref ShotHistory shotHistory)
		{
			try
			{
				XmlSerializer serializer = new XmlSerializer(typeof(ShotHistory));
				FileStream fs = new FileStream(strFilePath, FileMode.Open);
				shotHistory = (ShotHistory)serializer.Deserialize(fs);
				fs.Close();
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

		public static bool SaveToFile(string strFilePath, ShotHistory shotHistory)
		{
			try
			{
				XmlSerializer serializer = new XmlSerializer(typeof(ShotHistory));
				FileStream fs = new FileStream(strFilePath, FileMode.Create);
				serializer.Serialize(fs, shotHistory);
				fs.Close();
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

		public static bool LoadFromMemory(byte[] metaData, ref ShotHistory shotHistory)
		{
			try
			{
				XmlSerializer serializer = new XmlSerializer(typeof(ShotHistory));
				MemoryStream fs = new MemoryStream(metaData);
				shotHistory = (ShotHistory)serializer.Deserialize(fs);
				fs.Close();
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

		public static string Convert2Base64(ShotHistory shotHistory)
		{
			try
			{
				XmlSerializer serializer = new XmlSerializer(typeof(ShotHistory));
				MemoryStream fs = new MemoryStream();
				serializer.Serialize(fs, shotHistory);
				byte[] data = fs.ToArray();
				fs.Close();
				return Convert.ToBase64String(data);
			}
			catch (Exception)
			{
				return false.ToString();
			}
		}
	}
}
