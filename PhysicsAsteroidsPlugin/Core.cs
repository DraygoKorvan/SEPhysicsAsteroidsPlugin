using System;
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
			Console.WriteLine("PhysicsMeteoroidPlugin '" + Id.ToString() + "' initialized!");
			loadXML();
			m_control = true;
			m_running = true;
			if (events.Count == 0) 
				events.Add(new PhysicsMeteroidEvents());

			meteorcheck = new Thread(meteorScanLoop);
			meteorcheck.Start();

			controlloop = new Thread(meteorControlLoop);
			controlloop.Start();
		}

		#endregion

		#region "Properties"

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
		[Description("Enables or Disables meteoriod swarms")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool meteoron
		{
			get { return settings.meteorOn; }
			set { settings.meteorOn = value; }
		}
		[Category("Physics Meteoriod Plugin")]
		[Description("Enables or Disables meteoriod swarms")]
		[Browsable(true)]
		[ReadOnly(false)]
		public List<PhysicsMeteroidEvents> events
		{
			get { return settings.events; }
			set { settings.events = value; }
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
		public void velocityloop(FloatingObject obj, Vector3Wrapper vel, PhysicsMeteroidEvents _event)
		{
			Thread.Sleep(20);
			for (int count = 20; count > 0; count--)
			{
				if (obj.Mass > 0)
				{
					obj.MaxLinearVelocity = 104.7F * _event.maxVelocityFctr;
					obj.LinearVelocity = vel;

					debugWrite("Meteor entityID: " + obj.EntityId.ToString() + " Velocity: " + vel.ToString());
					
					break;
				}
				Thread.Sleep(20);
			}
			
			return;
		}
		private void showerPosition(PhysicsMeteroidEvents _event)
		{
			showerPosition(_event.location);
		}
		private void showerPosition(Vector3Wrapper pos, PhysicsMeteroidEvents _event = null)
		{
			if (events.First() == null)
				events.Add(new PhysicsMeteroidEvents());
			if (_event == null)
				_event = events.First();

			try
			{
				int ranmeteor = m_gen.Next(_event.maxMeteoroidAmt - _event.minMeteroidAmt) + _event.minMeteroidAmt;

				if (ranmeteor == 0) return;
				int largemeteor = m_gen.Next(ranmeteor);
				float vel = 0F;
				Vector3Wrapper intercept;
				Vector3Wrapper velvector;
				Vector3Wrapper spawnPos;
				//CubeGridEntity target = findTarget(true);
				//Vector3Wrapper pos = target.Position;
				Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), _event.spawnDistance));

				//spawn meteors in a random position around stormpos with the velocity of velnorm
				for (int i = 0; i < ranmeteor; i++)
				{
					Thread.Sleep(_event.spacingTimer);
					spawnPos = Vector3.Add(
							stormpos,
							Vector3.Multiply(
								new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1),
								100) //distance in meters for the spawn sphere
							);
					vel = (float)((50d + m_gen.NextDouble() * 55d) * _event.velocityFctr);
					if (vel > _event.maxVelocityFctr * 104.7F) vel = 104.7F * _event.maxVelocityFctr;

					intercept = FindInterceptVector(spawnPos, vel, pos, new Vector3Wrapper(0,0,0));
					velvector = Vector3.Add(intercept,
							Vector3.Multiply(Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), _event.spawnAcc)//randomize the vector by a small amount
							);
					spawnMeteor(spawnPos, velvector, _event, (i == largemeteor));
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
			if (events.First() == null)
				events.Add(new PhysicsMeteroidEvents());
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
				    PhysicsMeteroidEvents _event = new PhysicsMeteroidEvents();
					_event.oreAmt = 60000;
					_event.largeOreAmt = 600000;
					spawnMeteor(stormpos, intercept, _event, large);
					return;
			}
			else
			{
				//hunt for pilots!
				try
				{
					CockpitEntity targetcockpit = null;
					List<CubeGridEntity> grids = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
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
						spawnMeteor(stormpos, intercept, events.First(), large);
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
		private void createSectorStorm(PhysicsMeteroidEvents _event)
		{
			try
			{


				int ranmeteor = m_gen.Next(_event.maxMeteoroidAmt - _event.minMeteroidAmt) + _event.minMeteroidAmt;
				if (ranmeteor == 0) return;
				int largemeteor = m_gen.Next(ranmeteor);
				float vel = 0F;
				Vector3Wrapper intercept;
				Vector3Wrapper velvector;
				Vector3Wrapper spawnPos;
				List<CubeGridEntity> targets = findTargets(true);
				Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				foreach (CubeGridEntity target in targets)
				{

					Vector3Wrapper pos = target.Position;
					Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), _event.spawnDistance));

					//spawn meteors in a random position around stormpos with the velocity of velnorm
					for (int i = 0; i < ranmeteor; i++)
					{
						Thread.Sleep(_event.spacingTimer);
						spawnPos = Vector3.Add(
								stormpos,
								Vector3.Multiply(
									new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1),
									100) //distance in meters for the spawn sphere
								);
						vel = (float)((50d + m_gen.NextDouble() * 55d) * _event.velocityFctr);
						if (vel > _event.maxVelocityFctr * 104.7F) vel = 104.7F * _event.maxVelocityFctr;

						intercept = FindInterceptVector(spawnPos, vel, target.Position, target.LinearVelocity);
						velvector = Vector3.Add(intercept,
								Vector3.Multiply(Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), _event.spawnAcc)//randomize the vector by a small amount
								);
						spawnMeteor(spawnPos, velvector, _event, (i == largemeteor));
					}
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
		private void createMeteorStorm(PhysicsMeteroidEvents _event)
		{
			try
			{
				int ranmeteor = m_gen.Next(_event.maxMeteoroidAmt - _event.minMeteroidAmt) + _event.minMeteroidAmt;
				
				if (ranmeteor == 0) return;
				int largemeteor = m_gen.Next(ranmeteor);
				float vel = 0F;
				Vector3Wrapper intercept;
				Vector3Wrapper velvector;
				Vector3Wrapper spawnPos;
				CubeGridEntity target = findTarget(true);
				Vector3Wrapper pos = target.Position;
				Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), _event.spawnDistance));
				
				//spawn meteors in a random position around stormpos with the velocity of velnorm
				for (int i = 0; i < ranmeteor; i++)
				{
					Thread.Sleep(_event.spacingTimer);
					spawnPos = Vector3.Add(
							stormpos,
							Vector3.Multiply(
								new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1),
								100) //distance in meters for the spawn sphere
							);
					vel = (float)((50d + m_gen.NextDouble() * 55d) * _event.velocityFctr);
					if (vel > _event.maxVelocityFctr * 104.7F) vel = 104.7F * _event.maxVelocityFctr;
					intercept = FindInterceptVector(spawnPos, vel, target.Position, target.LinearVelocity);
					velvector = Vector3.Add(intercept,
							Vector3.Multiply(Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), _event.spawnAcc)//randomize the vector by a small amount
							);
					spawnMeteor(spawnPos, velvector, _event, (i == largemeteor));
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
		private List<long> generateTargetList(List<ulong> playerList)
		{
			if (playerList.Count() <= 0) 
				throw new PMNoPlayersException("No players found");
			List<long> list = new List<long>();

			foreach (ulong steamid in playerList)
			{
				list.AddRange(getOwnerFromSteamId(steamid));
			}
			if(list.Count() <= 0 )
				throw new PMNoTargetException("Invalid Target");
			return list;
		}
		private List<long> getOwnerFromSteamId(ulong steamid)
		{
			return PlayerMap.Instance.GetPlayerIdsFromSteamId(steamid);
		}
		private List<CubeGridEntity> findTargets(bool targetplayer = true)
		{
			//pull online players
			List<ulong> playerList = ServerNetworkManager.Instance.GetConnectedPlayers();
			int connectedPlayers = playerList.Count;
			if (playerList.Count <= 0)
				throw new PMNoPlayersException("No players on server aborting.");
			List<CubeGridEntity> targets = new List<CubeGridEntity>();
			List<CubeGridEntity> finalTargets = new List<CubeGridEntity>();
			int targetno = 0;
			foreach ( ulong player in playerList)
			{

				targets.Clear();
				List<long> targetowner = new List<long>();
				//convert utarget to target, target is just an id. not supported in API yet
				try
				{
					targetowner.AddRange(getOwnerFromSteamId(player));
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
								if (cubeBlock.Owner > 0)
								{

									if (targetowner.Contains(cubeBlock.Owner))
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
				if (targets.Count == 0) continue;
				targetno = m_gen.Next(targets.Count());
				if (targetno > targets.Count()) continue;
				debugWrite("Selected target entityID: " + targets[targetno].EntityId.ToString());
				finalTargets.Add(targets[targetno]);
			}

			return finalTargets;
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
			List<long> targetowner = new List<long>();
			//convert utarget to target, target is just an id. not supported in API yet
			try
			{
				targetowner = generateTargetList(playerList);
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

								if (targetowner.Contains(cubeBlock.Owner))
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
		private void spawnMeteor(Vector3Wrapper spawnpos, Vector3Wrapper vel, Vector3Wrapper up, Vector3Wrapper forward, PhysicsMeteroidEvents _event, bool large = false)
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
				tempitem.AmountDecimal = Math.Round((decimal)(_event.oreAmt * getOreFctr(tempitem.PhysicalContent.SubtypeName) * m_ore_fctr));
			else
				tempitem.AmountDecimal = Math.Round((decimal)(_event.largeOreAmt * getOreFctr(tempitem.PhysicalContent.SubtypeName) * m_ore_fctr));
			if (tempitem.AmountDecimal < (decimal)0.01d) tempitem.AmountDecimal = (decimal)0.01d;
			tempitem.ItemId = 0;

			tempobject = (MyObjectBuilder_FloatingObject)MyObjectBuilder_FloatingObject.CreateNewObject(m_FloatingObjectType);
			tempobject.Item = tempitem;

			physicsmeteor = new FloatingObject(tempobject);
			physicsmeteor.Up = up;
			physicsmeteor.Forward = forward;
			physicsmeteor.Position = spawnpos;
			physicsmeteor.LinearVelocity = vel;
			physicsmeteor.MaxLinearVelocity = 104.7F * _event.maxVelocityFctr;
			if (SandboxGameAssemblyWrapper.IsDebugging)
			{
				LogManager.APILog.WriteLineAndConsole("Meteor entityID: " + physicsmeteor.EntityId.ToString() + " Velocity: " + vel.ToString());
			}
			SectorObjectManager.Instance.AddEntity(physicsmeteor);
			//workaround for the velocity problem.
			Thread physicsthread = new Thread(() => velocityloop(physicsmeteor, vel, _event));
			physicsthread.Start();	

		}
		private void spawnMeteor(Vector3Wrapper spawnpos, Vector3Wrapper vel, PhysicsMeteroidEvents _event, bool large = false)
		{
			spawnMeteor(spawnpos, vel, new Vector3Wrapper(0F,1F,0F), new Vector3Wrapper(0F,0F,-1F), _event, large);
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
				return "Cobalt";
			if (m_gen.NextDouble() > 0.5d)
				return "Nickel";
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
				case "Nickel": return 1d;
				case "Cobalt": return 1d;
			}
			return 1d;
		}
		private void meteorControlLoop()
		{

			while (m_control)
			{
				Thread.Sleep(1000);
				try
				{
					foreach (PhysicsMeteroidEvents _event in events)
					{
						if (_event.lastrun == null)
						{
							_event.lastrun = DateTime.UtcNow;
							_event.nextrun = DateTime.UtcNow + TimeSpan.FromSeconds(_event.interval + (m_gen.NextDouble() * _event.randInterval * 2) - _event.randInterval);
						}
						if (DateTime.UtcNow > _event.nextrun)
						{
							_event.lastrun = DateTime.UtcNow;
							_event.nextrun = DateTime.UtcNow + TimeSpan.FromSeconds(_event.interval + (m_gen.NextDouble() * _event.randInterval * 2) - _event.randInterval);
							if (_event.enabled)
							{
								switch (_event.eventType)
								{

									case "Individual":
										createMeteorStorm(_event);
										break;
									case "Sector":
										if (_event.warningMessage != null && _event.warn)
										{
											ChatManager.Instance.SendPublicChatMessage(_event.warningMessage);
										}
										createMeteorStorm(_event);
										break;
									case "Location":
										showerPosition(_event);
										break;
								}
							}

						}
					}
				}
				catch (InvalidOperationException)
				{
					//do nothing can trigger if the collection is modified during this loop. 
				}
				catch (Exception ex)
				{
					LogManager.APILog.WriteLine(ex);
				}
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
			if (events.First() == null)
				events.Add(new PhysicsMeteroidEvents());

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
							events.First().oreAmt = Convert.ToDouble(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Ore amount set to " + events.First().oreAmt.ToString());
						}
						if (words[1] == "pm-largeore")
						{
							events.First().largeOreAmt = Convert.ToDouble(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Ore amount set to " + events.First().largeOreAmt.ToString());
						}
						if (words[1] == "pm-interval")
						{
							events.First().interval = Convert.ToInt32(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Meteroid storm interval set to " + events.First().interval.ToString());
						}
						if (words[1] == "pm-randominterval")
						{
							events.First().randInterval = Convert.ToInt32(rem.Trim());
							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Meteroid storm random interval set to " + events.First().randInterval.ToString());
						}
						if (words[1] == "pm-type")
						{
							if (words[2] == "sector")
							{
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Meteoroid storms set to sector wide");
								events.First().eventType = "Sector";
								return;
							}

							if (words[2] == "individual")
							{
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Meteoroid storms set to individual");
								events.First().eventType = "Individual";
								return;
							}

							ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Invalid parameter, please specify individual or sector /set pm-type [individual|sector]");
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
					Thread t = new Thread(() => createMeteorStorm(events.First()));
					t.Start();
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Starting meteoriod storm");
					return;
				}
				if (isadmin && words[0] == "/pm-sectorwave")
				{
					Thread t = new Thread(() => createSectorStorm(events.First()));
					t.Start();
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Starting meteoriod sector wide storm");
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

				if (isadmin && words[0] == "/pm-enablewarning")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Sector Meteroid Warning Enabled");
					events.First().warn = true;
					return;
				}

				if (isadmin && words[0] == "/pm-disablewarning")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Sector Meteroid Warning Disabled");
					events.First().warn = false;
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
