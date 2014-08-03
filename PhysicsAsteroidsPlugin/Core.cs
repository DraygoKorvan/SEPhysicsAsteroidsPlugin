﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Timers;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.VRageData;

using SEModAPIExtensions.API.Plugin;
using SEModAPIExtensions.API.Plugin.Events;
using SEModAPIExtensions.API;

using SEModAPIInternal.API.Common;
using SEModAPIInternal.API.Entity;
using SEModAPIInternal.API.Entity.Sector.SectorObject;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid.CubeBlock;
using SEModAPIInternal.API.Server;
using SEModAPIInternal.Support;

using SEModAPI.API;

using VRageMath;
using VRage.Common.Utils;





namespace PhysicsMeteroidsPlugin
{
	[Serializable()]
	public class PhysicsMeteroidSettings
	{
		private double m_oreAmt = 6000;
		private double m_largeOreAmt = 60000;

		private float m_maxVelocityFctr = 1F;
		private float m_velocityFctr = 3F;

		private UInt32 m_spawnDistance = 3000;
		private float m_spawnAcc = 0.2F;
		private int m_maxMeteoroidAmt = 10;
		private int m_minMeteoroidAmt = 1;
		private int m_interval = 300;
		private int m_ranInterval = 60;
		private bool m_meteorOn = true;
		private int m_spacingTimer = 100;

		public double oreAmt
		{
			get { return m_oreAmt; }
			set { if (value > 0.01d) m_oreAmt = value; }
		}
		public double largeOreAmt
		{
			get { return m_largeOreAmt; }
			set { if (value > 1d) m_largeOreAmt = value; }
		}
		public float maxVelocityFctr
		{
			get { return m_maxVelocityFctr; }
			set {if (value > 0) m_maxVelocityFctr = value;  }
		}
		public float velocityFctr
		{
			get { return m_velocityFctr; }
			set {  if (value > 0) m_velocityFctr = value; }
		}
		public UInt32 spawnDistance
		{
			get { return m_spawnDistance; }
			set { if(value >= 10) m_spawnDistance = value; }
		}
		public float spawnAcc
		{
			get { return m_spawnAcc; }
			set { if (value >= 0) m_spawnAcc = value; }
		}
		public int maxMeteoroidAmt
		{
			get { return m_maxMeteoroidAmt;  }
			set { if(value >= m_minMeteoroidAmt) m_maxMeteoroidAmt = value; }
		}
		public int minMeteoroidAmt
		{
			get { return m_minMeteoroidAmt; }
			set { if( value <= m_maxMeteoroidAmt) m_minMeteoroidAmt = value; }
		}
		public int interval
		{
			get { return m_interval; }
			set { if (value >= 30) m_interval = value; }
		}
		public int randInterval
		{
			get { return m_ranInterval; }
			set { if( value >= 0) m_ranInterval = value; }
		}
		public bool meteorOn
		{
			get { return m_meteorOn; }
			set { m_meteorOn = value; }
		}
		public int spacingTimer
		{
			get { return m_spacingTimer; }
			set { if (value > 10) m_spacingTimer = value; else m_spacingTimer = 10; }
		}
	}

	public class PhysicsMeteroidCore : PluginBase, IChatEventHandler
	{
		
		#region "Attributes"

		PhysicsMeteroidSettings settings;
		
		private bool m_running = false;
		private bool m_control = false;
		private double m_ore_fctr = 1;


		private Thread meteorcheck;
		private Thread controlloop;

		private static Type m_InventoryItemType = new MyObjectBuilderType(typeof(MyObjectBuilder_InventoryItem));
		private static Type m_OreType = new MyObjectBuilderType(typeof(MyObjectBuilder_Ore));
		private static Type m_FloatingObjectType = new MyObjectBuilderType(typeof(MyObjectBuilder_FloatingObject));
		
		private Random m_gen;

		#endregion

		#region "Constructors and Initializers"

		public void Core()
		{
			Console.WriteLine("PhysicsMeteoroidPlugin '" + Id.ToString() + "' constructed!");	
		}

		public override void Init()
		{
			settings = new PhysicsMeteroidSettings();
			m_running = false;
			m_control = false;
			m_gen = new Random(3425325);//temp hash
			settings.maxVelocityFctr = 1F;
			settings.velocityFctr = 3F;
			settings.oreAmt = 60000;
			settings.largeOreAmt = 60000;
			settings.maxMeteoroidAmt = 10;
			settings.minMeteoroidAmt = 1;
			settings.spawnDistance = 3000;
			settings.spawnAcc = 3.0F;
			settings.meteorOn = true;
			settings.spacingTimer = 100;
			Console.WriteLine("PhysicsMeteoroidPlugin '" + Id.ToString() + "' initialized!");
			loadXML();

			meteorcheck = new Thread(meteorScanLoop);
			meteorcheck.Start();

			controlloop = new Thread(meteorControlLoop);
			controlloop.Start();
		}

		#endregion

		#region "Properties"

		[Category("Physics Meteoriod Plugin")]
		[Description("Maximum meteoriod size for regular meteroids.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public double ore_amt
		{
			get { return settings.oreAmt; }
			set { if (value > 0) settings.oreAmt = value; }
		}
		[Category("Physics Meteoriod Plugin")]
		[Description("Maximum meteoriod size for large meteroids.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public double large_ore_amt
		{
			get { return settings.largeOreAmt; }
			set { if (value > 0) settings.largeOreAmt = value; }
		}
		[Browsable(true)]
		[ReadOnly(true)]
		public string DefaultLocation
		{
			get { return System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\"; }

		}
		[Browsable(true)]
		[ReadOnly(true)]
		public string Location
		{
			get { return SandboxGameAssemblyWrapper.Instance.GetServerConfig().LoadWorld + "\\"; }

		}

		[Category("Utils")]
		[Description("MeteorScan Loop On")]
		[Browsable(true)]
		[ReadOnly(true)]
		public bool MeteorScanLoop
		{
			get { return m_running; }

		}

		[Category("Utils")]
		[Description("Control Loop on")]
		[Browsable(true)]
		[ReadOnly(true)]
		public bool MeteorControlLoop
		{
			get { return m_control; }

		}

		[Category("Utils")]
		[Description("Meteor Scan Loop Status")]
		[Browsable(true)]
		[ReadOnly(true)]
		public string MeteorScanLoopThreadState
		{
			get { return meteorcheck.ThreadState.ToString(); }

		}

		[Category("Utils")]
		[Description("Meteor Control Loop Status")]
		[Browsable(true)]
		[ReadOnly(true)]
		public string MeteorControlLoopThreadState
		{
			get { return controlloop.ThreadState.ToString(); }

		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Set Maximum velocity (does not work yet)")]
		[Browsable(true)]
		[ReadOnly(false)]
		
		public float maxVelocityFctr
		{
			get { return settings.maxVelocityFctr; }
			set { settings.maxVelocityFctr = value; }
		}
		[Category("Physics Meteoriod Plugin")]
		[Description("Multiply original velocity by this factor.")]
		[Browsable(true)]
		[ReadOnly(false)]
		
		public float velocityFctr
		{
			get { return settings.velocityFctr; }
			set { settings.velocityFctr = value; }
		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Enables or Disables meteoriod swarms")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool meteoron
		{
			get { return settings.meteorOn; }
			set { settings.meteorOn = value; }
		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Maximium amount of meteoroids to spawn in each wave.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int max_meteoramt
		{
			get { return settings.maxMeteoroidAmt; }
			set { settings.maxMeteoroidAmt = value; }
		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Minimum amount of meteoroids to spawn in each wave.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int min_meteoramt
		{
			get { return settings.minMeteoroidAmt; }
			set	{ settings.minMeteoroidAmt = value; }
		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Interval in seconds between each meteor wave.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int interval
		{
			get { return settings.interval; }
			set { settings.interval = value; }

		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Interval is added to or subtracted from this amount. interval of 120 and a randinterval of 30, means a wave can spawn every 90 to 150 seconds.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int randinterval
		{
			get { return settings.randInterval; }
			set { settings.randInterval = value; }

		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Distance Meteroids spawn at, closer is more accurate.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public UInt32 spawnDistance 
		{
			get { return settings.spawnDistance; }
			set { settings.spawnDistance = value; }

		}
		[Category("Physics Meteoriod Plugin")]
		[Description("How accurate meteroids are, 0 is perfect, larger is less accurate.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public float spawnAcc
		{
			get { return settings.spawnAcc; }
			set { settings.spawnAcc = value; }

		}
		[Category("Physics Meteoriod Plugin")]
		[Description("Time between meteoroids in a storm.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int spacingTimer
		{
			get { return settings.spacingTimer; }
			set { settings.spacingTimer = value; }

		}
		#endregion

		#region "Methods"
		private void debugWrite(string message)
		{
			if (SandboxGameAssemblyWrapper.IsDebugging)
				LogManager.APILog.WriteLineAndConsole(message);
		}
		public void saveXML()
		{

			XmlSerializer x = new XmlSerializer(typeof(PhysicsMeteroidSettings));
			TextWriter writer = new StreamWriter(Location + "PhysicsMeteroid-Settings.xml");
			x.Serialize(writer, settings);
			writer.Close();

		}
		public void loadXML(bool l_default)
		{
			try
			{
				if (File.Exists(Location + "PhysicsMeteroid-Settings.xml") && !l_default)
				{

					XmlSerializer x = new XmlSerializer(typeof(PhysicsMeteroidSettings));
					TextReader reader = new StreamReader(Location + "PhysicsMeteroid-Settings.xml");
					PhysicsMeteroidSettings obj = (PhysicsMeteroidSettings)x.Deserialize(reader);
					settings = obj;
					reader.Close();
					return;
				}
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole("Could not load configuration: " + ex.ToString());
			}
			try
			{
				if (File.Exists(DefaultLocation + "PhysicsMeteroid-Settings.xml"))
				{
					XmlSerializer x = new XmlSerializer(typeof(PhysicsMeteroidSettings));
					TextReader reader = new StreamReader(DefaultLocation + "PhysicsMeteroid-Settings.xml");
					PhysicsMeteroidSettings obj = (PhysicsMeteroidSettings)x.Deserialize(reader);
					settings = obj;
					reader.Close();
				}
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole("Could not load configuration: " + ex.ToString());
			}

		}
		public void loadXML()
		{
			loadXML(false);
		}
		public void velocityloop(FloatingObject obj, Vector3Wrapper vel)
		{
			Thread.Sleep(20);
			for (int count = 20; count > 0; count--)
			{
				if (obj.Mass > 0)
				{
					obj.MaxLinearVelocity = 104.7F * maxVelocityFctr;
					obj.LinearVelocity = vel;

					debugWrite("Meteor entityID: " + obj.EntityId.ToString() + " Velocity: " + vel.ToString());
					
					break;
				}
				Thread.Sleep(20);
			}
			
			return;
		}
		private void showerPosition(Vector3Wrapper pos)
		{
			try
			{
				int ranmeteor = m_gen.Next(max_meteoramt - min_meteoramt) + min_meteoramt;

				if (ranmeteor == 0) return;
				int largemeteor = m_gen.Next(ranmeteor);
				float vel = 0F;
				Vector3Wrapper intercept;
				Vector3Wrapper velvector;
				Vector3Wrapper spawnPos;
				//CubeGridEntity target = findTarget(true);
				//Vector3Wrapper pos = target.Position;
				Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), spawnDistance));

				//spawn meteors in a random position around stormpos with the velocity of velnorm
				for (int i = 0; i < ranmeteor; i++)
				{
					Thread.Sleep(spacingTimer);
					spawnPos = Vector3.Add(
							stormpos,
							Vector3.Multiply(
								new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1),
								100) //distance in meters for the spawn sphere
							);
					vel = (float)((50d + m_gen.NextDouble() * 55d) * velocityFctr);
					if (vel > maxVelocityFctr * 104.7F) vel = 104.7F * maxVelocityFctr;

					intercept = FindInterceptVector(spawnPos, vel, pos, new Vector3Wrapper(0,0,0));
					velvector = Vector3.Add(intercept,
							Vector3.Multiply(Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), spawnAcc)//randomize the vector by a small amount
							);
					spawnMeteor(spawnPos, velvector, (i == largemeteor));
				}
			}
			catch (PMNoPlayersException)
			{
				//do nothing

				Console.WriteLine("Meteor Shower Aborted: No players on server");
			}
			catch (PMNoTargetException)
			{
				Console.WriteLine("Meteor Shower Aborted: Invalid Target");
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole(ex.ToString());
			}
		}
		private void smitePlayer(string playerName, bool large = false)
		{
			//get steamid from playername
			List<ulong> steamids = ServerNetworkManager.Instance.GetConnectedPlayers();
			ulong bestmatch = 0;
			ulong lcmatch = 0;
			ulong match = 0;
			string bestname = null;
			string lcname = null;
			string matchname = null;
			foreach (ulong id in steamids)
			{
				string name = PlayerMap.Instance.GetPlayerNameFromSteamId(id);
				if (name == playerName)
				{
					match = id;
					matchname = name;
				}
				if (name.ToLower() == playerName.ToLower())
				{
					lcmatch = id;
					lcname = name;
				}
				if(name.ToLower().Contains(playerName.ToLower()))
				{
					bestmatch = id;
					bestname = name;
				}
			}
			ulong target = 0;
			string foundname = null;
			if (bestmatch > 0)
			{
				target = bestmatch;
				foundname = bestname;
			}
			if (lcmatch > 0)
			{
				target = lcmatch;
				foundname = lcname;
			}

			if (match > 0)
			{
				target = match;
				foundname = matchname;
			}
			//have target, hopefully
			if (target == 0) throw new PMNoTargetException("Target not found.");

			long entityid = PlayerMap.Instance.GetPlayerEntityId(target);
			List<CharacterEntity> characters = SectorObjectManager.Instance.GetTypedInternalData<CharacterEntity>();
			CharacterEntity targetchar;
			try
			{
				targetchar = characters.Find(x => x.EntityId == entityid);
			}
			catch (ArgumentNullException)
			{
				targetchar = null;
			}
			
			if( targetchar != null)
			{
					Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
					Vector3Wrapper pos = targetchar.Position;
					Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), 200));//were smiting, fire close!
					Vector3Wrapper intercept = FindInterceptVector(stormpos, 202.0F, pos, targetchar.LinearVelocity);
					spawnMeteor(stormpos, intercept, large);
					return;
			}
			else
			{
				//hunt for pilots!
				try
				{
					CockpitEntity targetcockpit = null;
					List<CubeGridEntity> grids = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
					debugWrite(entityid.ToString());
					bool loop = true;
					foreach (CubeGridEntity grid in grids)
					{
						List<CubeBlockEntity> cubeblocks = grid.CubeBlocks;
						foreach (CubeBlockEntity cube in cubeblocks)
						{
							if(cube is CockpitEntity)
							{
								targetcockpit = (CockpitEntity)cube;
								if (targetcockpit.Pilot == null) continue;
								debugWrite(targetcockpit.Pilot.EntityId.ToString());
								debugWrite(targetcockpit.Pilot.DisplayName);
								if (targetcockpit.Pilot.DisplayName == foundname)
								{
									loop = false;
									break;
								}
								else
									targetcockpit = null;
								
							}
						}
						if (!loop) break;
					}

					if (targetcockpit != null)
					{
						Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
						Vector3Wrapper pos = targetcockpit.Parent.Position;
						Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), 200));//were smiting, fire close!
						Vector3Wrapper intercept = FindInterceptVector(stormpos, 202.0F, pos, targetcockpit.Parent.LinearVelocity);
						spawnMeteor(stormpos, intercept, large);
						return;
					}
				}
				catch (Exception ex)
				{
					LogManager.APILog.WriteLineAndConsole(ex + " " + ex.ToString() + " " + ex.StackTrace.ToString());
					throw ex;
				}
			}
			throw new PMNoTargetException("Could not find player.");
		}
		private void createMeteorStorm()
		{
			try
			{
				int ranmeteor = m_gen.Next(max_meteoramt - min_meteoramt) + min_meteoramt;
				
				if (ranmeteor == 0) return;
				int largemeteor = m_gen.Next(ranmeteor);
				float vel = 0F;
				Vector3Wrapper intercept;
				Vector3Wrapper velvector;
				Vector3Wrapper spawnPos;
				CubeGridEntity target = findTarget(true);
				Vector3Wrapper pos = target.Position;
				Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), spawnDistance));
				
				//spawn meteors in a random position around stormpos with the velocity of velnorm
				for (int i = 0; i < ranmeteor; i++)
				{
					Thread.Sleep(spacingTimer);
					spawnPos = Vector3.Add(
							stormpos,
							Vector3.Multiply(
								new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1),
								100) //distance in meters for the spawn sphere
							);
					vel = (float)((50d + m_gen.NextDouble() * 55d) * velocityFctr);
					if (vel > maxVelocityFctr * 104.7F) vel = 104.7F * maxVelocityFctr;

					intercept = FindInterceptVector(spawnPos, vel, target.Position, target.LinearVelocity);
					velvector = Vector3.Add(intercept,
							Vector3.Multiply(Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), spawnAcc)//randomize the vector by a small amount
							);
					spawnMeteor(spawnPos, velvector, (i == largemeteor));
				}
			}
			catch (PMNoPlayersException)
			{
				//do nothing
				
				Console.WriteLine("Meteor Shower Aborted: No players on server");
			}
			catch (PMNoTargetException)
			{
				Console.WriteLine("Meteor Shower Aborted: Invalid Target");
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole(ex.ToString());
			}
		}

		//Ref Danik from DanikGames http://danikgames.com/blog/?p=809
		private Vector3 FindInterceptVector(Vector3 spawnOrigin, float meteoroidSpeed, Vector3 targetOrigin, Vector3 targetVel)
		{

			Vector3 dirToTarget = Vector3.Normalize(targetOrigin - spawnOrigin);
			Vector3 targetVelOrth = Vector3.Dot(targetVel, dirToTarget) * dirToTarget;
			Vector3 targetVelTang = targetVel - targetVelOrth;
			Vector3 shotVelTang = targetVelTang;

			// Now all we have to find is the orthogonal velocity of the shot

			float shotVelSpeed = shotVelTang.Length();
			if (shotVelSpeed > meteoroidSpeed)
			{
				return Vector3.Multiply(targetVel, meteoroidSpeed);
			}
			else
			{
				float shotSpeedOrth = (float)Math.Sqrt(meteoroidSpeed * meteoroidSpeed - shotVelSpeed * shotVelSpeed);
				Vector3 shotVelOrth = dirToTarget * shotSpeedOrth;
				return shotVelOrth + shotVelTang;
			}
		}
		private ulong pickPlayer(List<ulong> playerList)
		{
			
			//select a target
			if (playerList.Count() <= 0)
				throw new PMNoPlayersException("No players found");
			int targetno = m_gen.Next(playerList.Count());
			debugWrite("Selected index: " + targetno.ToString());
			if (targetno > playerList.Count() || targetno < 0) 
				throw new PMNoTargetException("Invalid Target");
			ulong utarget = playerList[targetno];
			debugWrite("target: " + utarget);
			return utarget;
		}
		private List<long> getOwnerFromSteamId(ulong steamid)
		{
			return PlayerMap.Instance.GetPlayerIdsFromSteamId(steamid);
		}

		private CubeGridEntity findTarget(bool targetplayer = true)
		{
			//pull online players
			List<ulong> playerList = ServerNetworkManager.Instance.GetConnectedPlayers();
			int connectedPlayers = playerList.Count;
			if (playerList.Count <= 0)
				throw new PMNoPlayersException("No players on server aborting.");
			List<CubeGridEntity> targets = new List<CubeGridEntity>();
			int targetno = 0;
			ulong utarget = 0;
			List<long> targetowner = new List<long>();
			//convert utarget to target, target is just an id. not supported in API yet
			try
			{
				utarget = pickPlayer(playerList);
				targetowner = getOwnerFromSteamId(utarget);
			}
			catch (PMNoTargetException)
			{
				//if were supposed to target players, throw. 
				if (targetplayer)
					throw;
			}
			catch (Exception)
			{
				throw;//throw any other exception
			}
			//set to targetid obtained from utarget

			List<CubeGridEntity> list = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
			foreach (var item in list)
			{
				try
				{
					if (item.CubeBlocks.Count <= 20) continue;
					if (!targetplayer)
					{
						targets.Add(item);
					}
					else
					{

						foreach (var cubeBlock in item.CubeBlocks)
						{
							if(cubeBlock.Owner > 0)
							{

								if (targetowner.Contains(cubeBlock.Owner) && item.CubeBlocks.Count > 20)
								{
									debugWrite("Target Found, adding to target list.");
									targets.Add(item);
									break;
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					LogManager.APILog.WriteLine(ex);
				}
			}
			if (targets.Count == 0) throw new PMNoTargetException("No targets availible");
			targetno = m_gen.Next(targets.Count());
			//debugWrite("Picked number " + targetno.ToString() + " in list");
			if (targetno > targets.Count()) throw new PMNoTargetException("Invalid Target");
			debugWrite("Selected target entityID: " + targets[targetno].EntityId.ToString());
			return targets[targetno];
		}
		private void spawnMeteor(Vector3Wrapper spawnpos, Vector3Wrapper vel, Vector3Wrapper up, Vector3Wrapper forward, bool large = false)
		{
			if (SandboxGameAssemblyWrapper.IsDebugging)
			{
				LogManager.APILog.WriteLineAndConsole("Physics Meteroid - spawnMeteor(" + spawnpos.ToString() + ", " + vel.ToString() + ", " + up.ToString() + ", " + forward.ToString() + ")" );
			}
			MyObjectBuilder_FloatingObject tempobject;
			MyObjectBuilder_Ore tempore = new MyObjectBuilder_Ore();
			MyObjectBuilder_InventoryItem tempitem = new MyObjectBuilder_InventoryItem();
			tempore.SetDefaultProperties();
			FloatingObject physicsmeteor;
			m_ore_fctr = m_gen.NextDouble();

			tempitem = (MyObjectBuilder_InventoryItem)MyObjectBuilder_InventoryItem.CreateNewObject(m_InventoryItemType);
			tempitem.PhysicalContent = (MyObjectBuilder_PhysicalObject)MyObjectBuilder_PhysicalObject.CreateNewObject(m_OreType);
			tempitem.PhysicalContent.SubtypeName = getRandomOre();
			if(!large)
				tempitem.AmountDecimal = Math.Round((decimal)(ore_amt * getOreFctr(tempitem.PhysicalContent.SubtypeName) * m_ore_fctr));
			else
				tempitem.AmountDecimal = Math.Round((decimal)(large_ore_amt * getOreFctr(tempitem.PhysicalContent.SubtypeName) * m_ore_fctr));
			if (tempitem.AmountDecimal < (decimal)0.01d) tempitem.AmountDecimal = (decimal)0.01d;
			tempitem.ItemId = 0;

			tempobject = (MyObjectBuilder_FloatingObject)MyObjectBuilder_FloatingObject.CreateNewObject(m_FloatingObjectType);
			tempobject.Item = tempitem;

			physicsmeteor = new FloatingObject(tempobject);
			physicsmeteor.Up = up;
			physicsmeteor.Forward = forward;
			physicsmeteor.Position = spawnpos;
			physicsmeteor.LinearVelocity = vel;
			physicsmeteor.MaxLinearVelocity = 104.7F * maxVelocityFctr;
			if (SandboxGameAssemblyWrapper.IsDebugging)
			{
				LogManager.APILog.WriteLineAndConsole("Meteor entityID: " + physicsmeteor.EntityId.ToString() + " Velocity: " + vel.ToString());
			}
			SectorObjectManager.Instance.AddEntity(physicsmeteor);
			//workaround for the velocity problem.
			Thread physicsthread = new Thread(() => velocityloop(physicsmeteor, vel));
			physicsthread.Start();	

		}
		private void spawnMeteor(Vector3Wrapper spawnpos, Vector3Wrapper vel, bool large = false)
		{
			spawnMeteor(spawnpos, vel, new Vector3Wrapper(0F,1F,0F), new Vector3Wrapper(0F,0F,-1F), large);
		}
		private string getRandomOre()
		{
			//next is twice as rare as the previous
			if (m_gen.NextDouble() > 0.5d)
				return "Stone";
			if (m_gen.NextDouble() > 0.5d)
				return "Iron";
			if (m_gen.NextDouble() > 0.5d)
				return "Silver";
			if (m_gen.NextDouble() > 0.5d)
				return "Silicon";
			if (m_gen.NextDouble() > 0.5d)
				return "Gold";
			if (m_gen.NextDouble() > 0.5d)
				return "Uranium";
			if (m_gen.NextDouble() > 0.5d)
				return "Platinum";
			return "Magnesium";
		}

		private double getOreFctr(string ore)
		{
			switch (ore)
			{
				case "Stone": return 1d;
				case "Iron": return 1d;
				case "Silver": return 0.9d;
				case "Silicon": return 1d;
				case "Gold": return 0.5d;
				case "Uranium": return 0.2d;
				case "Platinum": return 0.1d;
				case "Magnesium": return 0.05d;
			}
			return 1d;
		}
		private void meteorControlLoop()
		{
			m_control = true;
			while (m_control)
			{
				if(interval - randinterval > 30)
					Thread.Sleep((interval + (int)Math.Floor( (m_gen.NextDouble() * 2 - 1) * randinterval) ) * 1000 );
				else
					Thread.Sleep(30*1000);
				if(meteoron && m_control)
					createMeteorStorm();
			}
			return;
		}
		private void meteorScanLoop()
		{
			m_running = true;

			while (m_running)
			{
				try
				{
					clearMeteor();
				}
				catch (Exception ex)
				{
					LogManager.APILog.WriteLineAndConsole(ex.ToString());
				}
				Thread.Sleep(1000);
			}
		}
		public void clearMeteor()
		{

			int connectedPlayers = ServerNetworkManager.Instance.GetConnectedPlayers().Count;

			if (!meteoron || connectedPlayers == 0)
			{
				List<Meteor> entityList = SectorObjectManager.Instance.GetTypedInternalData<Meteor>();
				foreach (var sectorObject in entityList)
				{
					if (!sectorObject.IsDisposed)
					{
							sectorObject.Dispose();
					}
				}
				entityList.Clear();
			}

		}
		#region "EventHandlers"

		public override void Update()
		{
			
			//prevent multiple update threads to run at once.
			
		}

		public override void Shutdown()
		{
			LogManager.APILog.WriteLineAndConsole("Shutting Down Physics Meteoroid Plugin.");
			saveXML();
			//Thread.Sleep(300);//wait for functions to complete. 
			m_running = false;
			m_control = false;
			settings.meteorOn = false;
			//rejoin the threads, wait for them to terminate, if not force them to terminate.
			meteorcheck.Join(300);
			controlloop.Join(300);
			meteorcheck.Abort();
			controlloop.Abort();
			return;
		}

		public void OnChatReceived(ChatManager.ChatEvent obj)
		{

			if (obj.sourceUserId == 0)
				return;
			

			if (obj.message[0] == '/')
			{
				bool isadmin = SandboxGameAssemblyWrapper.Instance.IsUserAdmin(obj.sourceUserId);
				string[] words = obj.message.Split(' ');
				string rem = "";
				//proccess
				if (words.Count() > 2 && isadmin)
				{
					rem = String.Join(" ", words, 2, words.Count() - 2);
					if (words[0] == "/set")
					{
						
						if (words[1] == "pm-ore")
						{
							ore_amt = Convert.ToDouble(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Ore amount set to " + ore_amt.ToString());
						}
						if (words[1] == "pm-largeore")
						{
							large_ore_amt = Convert.ToDouble(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Ore amount set to " + large_ore_amt.ToString());
						}
						if (words[1] == "pm-interval")
						{
							interval = Convert.ToInt32(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Meteroid storm interval set to " + interval.ToString());
						}
						if (words[1] == "pm-randominterval")
						{
							randinterval = Convert.ToInt32(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Meteroid storm random interval set to " + randinterval.ToString());
						}
					}
				}
				if (isadmin && words[0] == "/pm-smite")
				{
					try
					{
						if (words.Count() > 2)
						{
							rem = String.Join(" ", words, 2, words.Count() - 2);
							if (words[1] == "small")
							{
								smitePlayer(rem, false);
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Dropping small rock on: " + rem);
							}
							if (words[1] == "large")
							{
								smitePlayer(rem, true);
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Dropping large rock on: " + rem);
							}
						}
						if (words.Count() > 1)
						{
							rem = String.Join(" ", words, 1, words.Count() - 1);
							smitePlayer(rem, false);
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Dropping small rock on: " + rem);
						}
					}
					catch (PMNoTargetException ex)
					{
						//ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Could not target player: " + ex.Message.ToString());
						debugWrite(ex.ToString());
					}

				}
				if (isadmin && words[0] == "/pm-spawnpos")
				{
					if(words.Count()>3)
					{
						int x = Convert.ToInt32(words[1]);
						int y = Convert.ToInt32(words[2]);
						int z = Convert.ToInt32(words[3]);
						Thread t = new Thread(() => showerPosition(new Vector3Wrapper(x,y,z)));
						t.Start();
						ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Starting meteoriod storm at " + x.ToString() + " " + y.ToString() + " " + z.ToString());
					}
				}
				if (isadmin && words[0] == "/pm-spawnwave")
				{
					Thread t = new Thread(createMeteorStorm);
					t.Start();
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Starting meteoriod storm");
					return;
				}
				if (isadmin && words[0] == "/pm-enable")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Automatic Meteoroid storms enabled");
					settings.meteorOn = true;
					return;
				}

				if (isadmin && words[0] == "/pm-disable")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Automatic Meteoroid storms disabled");
					settings.meteorOn = false;
					return;
				}

				if (isadmin && words[0] == "/pm-save")
				{
					
					saveXML();
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Physics Meteoroid Configuration Saved.");
					return;
				}
				if (isadmin && words[0] == "/pm-load")
				{
					loadXML(false);
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Physics Meteoroid Configuration Loaded.");
					return;
				}
				if (isadmin && words[0] == "/pm-loaddefault")
				{
					loadXML(true);
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Physics Meteoroid Configuration Defaults Loaded.");
					return;
				}
			}
			return;
		}
		public void OnChatSent(SEModAPIExtensions.API.ChatManager.ChatEvent obj)
		{
			//do nothing
			return;
		}
		#endregion



		#endregion
	}
}
