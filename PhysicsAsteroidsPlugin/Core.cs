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
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.Engine.Networking;
using Sandbox.ModAPI;

using SEModAPIExtensions.API.Plugin;
using SEModAPIExtensions.API.Plugin.Events;
using SEModAPIExtensions.API;


using SEModAPIInternal.API.Common;
using SEModAPIInternal.API.Entity;
using SEModAPIInternal.API.Entity.Sector.SectorObject;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid.CubeBlock;
using SEModAPIInternal.API.Server;
using SEModAPI.API.TypeConverters;
using SEModAPIInternal.Support;

using SEModAPI.API;
using SEModAPIInternal.API;
using SEModAPI;

using VRageMath;
using VRage.Common.Utils;
using VRage;
using VRage.Utils;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game;
//using Sandbox.Common.ObjectBuilders;
using VRage.FileSystem;
using NLog;






namespace PhysicsMeteroidsPlugin
{
	public class PhysicsMeteroidCore : PluginBase, IChatEventHandler
	{
		public static Logger Log;
		#region "Attributes"

		PhysicsMeteroidSettings settings;
		
		private bool m_running = false;
		private bool m_control = false;
		private double m_ore_fctr = 1;


		private Thread meteorcheck;
		private Thread controlloop;

		//private static Type m_InventoryItemType = new MyObjectBuilderType(typeof(MyObjectBuilder_InventoryItem));
		//private static Type m_OreType = new MyObjectBuilderType(typeof(MyObjectBuilder_Ore));
		//private static Type m_FloatingObjectType = new MyObjectBuilderType(typeof(MyObjectBuilder_FloatingObject));
		//private static Type m_MeteorObjectType = new MyObjectBuilderType(typeof(MyObjectBuilder_Meteor));

		private Random m_gen;

		//debug

		private bool m_debug = false;

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
			m_gen = new Random((int)System.DateTime.UtcNow.ToBinary());
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
			get { return MySession.Static.CurrentPath + "\\"; }
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

		[Category("Utils")]
		[Description("Debug status")]
		[Browsable(true)]
		[ReadOnly(true)]
		public bool isdebugging
		{
			get { return m_debug /*|| SandboxGameAssemblyWrapper.IsDebugging*/; }
		}

		[Category("Utils")]
		[Description("Set debugging")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool debug
		{
			get { return m_debug; }
			set { m_debug = value; }
		}
		#endregion

		#region "Methods"
		private void debugWrite(string message)
		{
			if (isdebugging)
			{
				Log.Debug(message);
				Console.WriteLine(message);
			}
				

		}

		public void saveXML()
		{

			XmlSerializer x = new XmlSerializer(typeof(PhysicsMeteroidSettings));
			TextWriter writer = new StreamWriter(Location + "PhysicsMeteroid-Settings.xml");
			x.Serialize(writer, settings);
			writer.Close();

		}
		public void loadXML(bool l_default = false)
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
				Console.WriteLine("Could not load configuration: " + ex.ToString());
				Log.Info("Could not load configuration: " + ex.ToString());
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
				Console.WriteLine("Could not load configuration: " + ex.ToString());
				Log.Warn("Could not load configuration: " + ex.ToString());
			}

		}

		private void showerPosition(PhysicsMeteroidEvents _event)
		{
			showerPosition(_event.location);
		}
		private void showerPosition(Vector3D pos, PhysicsMeteroidEvents _event = null)
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
				Vector3D intercept;
				Vector3D velvector;
				Vector3D spawnPos;
				Vector3D velnorm = Vector3D.Normalize(new Vector3D(m_gen.NextDouble() * 2 - 1, m_gen.NextDouble() * 2 - 1, m_gen.NextDouble() * 2 - 1));
				Vector3D stormpos = Vector3D.Add(pos, Vector3D.Multiply(Vector3D.Negate(velnorm), _event.spawnDistance));

				//spawn meteors in a random position around stormpos with the velocity of velnorm
				for (int i = 0; i < ranmeteor; i++)
				{
					Thread.Sleep(_event.spacingTimer);
					spawnPos = Vector3.Add(
							stormpos,
							Vector3.Multiply(
								new Vector3D(m_gen.NextDouble() * 2 - 1, m_gen.NextDouble() * 2 - 1, m_gen.NextDouble() * 2 - 1),
								100) //distance in meters for the spawn sphere
							);
					vel = (float)((50d + m_gen.NextDouble() * 55d) * _event.velocityFctr);
					if (vel > _event.maxVelocityFctr * 104.7F) vel = 104.7F * _event.maxVelocityFctr;

					intercept = FindInterceptVector(spawnPos, vel, pos, new Vector3D(0,0,0));
					velvector = Vector3D.Add(intercept,
							Vector3D.Multiply(Vector3D.Normalize(new Vector3D(m_gen.NextDouble() * 2 - 1, m_gen.NextDouble() * 2 - 1, m_gen.NextDouble() * 2 - 1)), _event.spawnAcc)//randomize the vector by a small amount
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
				Log.Warn(ex.ToString());
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
					Vector3D velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
					Vector3D pos = targetchar.Position;
					Vector3D stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), 200));//were smiting, fire close!
					Vector3D intercept = FindInterceptVector(stormpos, 202.0F, pos, (Vector3)targetchar.LinearVelocity);
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
								if (targetcockpit.PilotEntity == null) continue;
								if (targetcockpit.PilotEntity.DisplayName == foundname)
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

						Vector3I blockGridPos = targetcockpit.Position;
						Matrix matrix = targetcockpit.Parent.PositionAndOrientation.GetMatrix();
						Matrix orientation = matrix.GetOrientation();
						Vector3 rotatedBlockPos = Vector3.Transform((Vector3)blockGridPos * (targetcockpit.Parent.GridSizeEnum == MyCubeSize.Large ? 2.5f : 0.5f), orientation);
						Vector3D pos = rotatedBlockPos + (Vector3D)targetcockpit.Parent.Position;

						Vector3D stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), 200));//were smiting, fire close!
						Vector3D intercept = FindInterceptVector(stormpos, 202.0F, pos, (Vector3)targetcockpit.Parent.LinearVelocity);
						spawnMeteor(stormpos, intercept, events.First(), large);
						return;
					}
				}
				catch (Exception ex)
				{
					Log.Warn(ex + " " + ex.ToString() + " " + ex.StackTrace.ToString());
					throw ex;
				}
			}
			throw new PMNoTargetException("Could not find player.");
		}
		private void createSectorStorm(PhysicsMeteroidEvents _event)
		{
			try
			{
				float vel = 0F;
				Vector3D intercept;
				Vector3D velvector;
				Vector3D spawnPos;
				List<CubeGridEntity> targets = findTargets(true);
				Vector3D velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				foreach (CubeGridEntity target in targets)
				{
					int ranmeteor = m_gen.Next(_event.maxMeteoroidAmt - _event.minMeteroidAmt) + _event.minMeteroidAmt;
					if (ranmeteor == 0) continue;
					int largemeteor = m_gen.Next(ranmeteor);
					Vector3DWrapper pos = target.Position;

					CubeBlockEntity m_targetBlock = target.CubeBlocks.ElementAt(m_gen.Next(target.CubeBlocks.Count));

					Vector3I blockGridPos = m_targetBlock.Position;
					Matrix matrix = m_targetBlock.Parent.PositionAndOrientation.GetMatrix();
					Matrix orientation = matrix.GetOrientation();
					Vector3D rotatedBlockPos = Vector3.Transform((Vector3)blockGridPos * (target.GridSizeEnum == MyCubeSize.Large ? 2.5f : 0.5f), orientation);
					pos = rotatedBlockPos + pos;

					Vector3D stormpos = Vector3D.Add(pos, Vector3D.Multiply(Vector3D.Negate(velnorm), _event.spawnDistance));

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

						intercept = FindInterceptVector(spawnPos, vel, target.Position, (Vector3)target.LinearVelocity);
						velvector = Vector3.Add(intercept,
								Vector3.Multiply(Vector3.Normalize(new Vector3D((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), _event.spawnAcc)//randomize the vector by a small amount
								);
						spawnMeteor(spawnPos, velvector, _event, (i == largemeteor));
					}
				}
			}
			catch (PMNoPlayersException)
			{
				//do nothing

				Log.Info("Meteor Shower Aborted: No players on server");
			}
			catch (PMNoTargetException)
			{
				Log.Info("Meteor Shower Aborted: Invalid Target");
			}
			catch (Exception ex)
			{
				Log.Warn(ex.ToString());
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
				Vector3D intercept;
				Vector3D velvector;
				Vector3D spawnPos;
				CubeGridEntity target = findTarget(true);
				Vector3D pos = target.Position;
				//target a random block
				CubeBlockEntity m_targetBlock = target.CubeBlocks.ElementAt(m_gen.Next(target.CubeBlocks.Count));
							
				Vector3I blockGridPos = m_targetBlock.Position;
				Matrix matrix = target.PositionAndOrientation.GetMatrix();
				Matrix orientation = matrix.GetOrientation();
				Vector3 rotatedBlockPos = Vector3.Transform((Vector3)blockGridPos * (target.GridSizeEnum == MyCubeSize.Large ? 2.5f : 0.5f), orientation);
				pos = rotatedBlockPos + pos;

				Vector3D velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				Vector3D stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), _event.spawnDistance));
				
				//spawn meteors in a random position around stormpos with the velocity of velnorm
				for (int i = 0; i < ranmeteor; i++)
				{
					Thread.Sleep(_event.spacingTimer);
					spawnPos = Vector3D.Add(
							stormpos,
							Vector3D.Multiply(
								new Vector3D((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1),
								100) //distance in meters for the spawn sphere
							);
					vel = (float)((50d + m_gen.NextDouble() * 55d) * _event.velocityFctr);
					if (vel > _event.maxVelocityFctr * 104.7F) vel = 104.7F * _event.maxVelocityFctr;
					intercept = FindInterceptVector(spawnPos, vel, target.Position, (Vector3)target.LinearVelocity);
					velvector = Vector3.Add(intercept,
							Vector3.Multiply(Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), _event.spawnAcc)//randomize the vector by a small amount
							);
					spawnMeteor(spawnPos, velvector, _event, (i == largemeteor));
				}
			}
			catch (PMNoPlayersException)
			{
				//do nothing
				
				Log.Info("Meteor Shower Aborted: No players on server");
			}
			catch (PMNoTargetException)
			{
				Log.Info("Meteor Shower Aborted: Invalid Target");
			}
			catch (Exception ex)
			{
				Log.Warn(ex.ToString());
			}
		}

		//Ref Danik from DanikGames http://danikgames.com/blog/?p=809
		private Vector3D FindInterceptVector(Vector3D spawnOrigin, double meteoroidSpeed, Vector3D targetOrigin, Vector3D targetVel)
		{

			Vector3D dirToTarget = Vector3D.Normalize(targetOrigin - spawnOrigin);
			Vector3D targetVelOrth = Vector3D.Dot(targetVel, dirToTarget) * dirToTarget;
			Vector3D targetVelTang = targetVel - targetVelOrth;
			Vector3D shotVelTang = targetVelTang;

			// Now all we have to find is the orthogonal velocity of the shot

			double shotVelSpeed = shotVelTang.Length();
			if (shotVelSpeed > meteoroidSpeed)
			{
				return Vector3D.Multiply(targetVel, meteoroidSpeed);
			}
			else
			{
				double shotSpeedOrth = (double)Math.Sqrt(meteoroidSpeed * meteoroidSpeed - shotVelSpeed * shotVelSpeed);
				Vector3D shotVelOrth = dirToTarget * shotSpeedOrth;
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
						Log.Warn(ex);
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
					Log.Warn(ex);
				}
			}
			if (targets.Count == 0) throw new PMNoTargetException("No targets availible");
			targetno = m_gen.Next(targets.Count());
			//debugWrite("Picked number " + targetno.ToString() + " in list");
			if (targetno > targets.Count()) throw new PMNoTargetException("Invalid Target");
			debugWrite("Selected target entityID: " + targets[targetno].EntityId.ToString());
			return targets[targetno];
		}
		private void spawnMeteor(Vector3D spawnpos, Vector3D vel, Vector3 up, Vector3 forward, PhysicsMeteroidEvents _event, bool large = false)
		{

			debugWrite("Physics Meteroid - spawnMeteor(" + spawnpos.ToString() + ", " + vel.ToString() + ", " + up.ToString() + ", " + forward.ToString() + ")" );
			
			if (_event.keenMeteoroid)
			{

				MyFixedPoint amount = 0;

				m_ore_fctr = m_gen.NextDouble();

				string randorename = getRandomOre();

				if (!large)
					amount = (MyFixedPoint)Math.Round((decimal)(_event.oreAmt * getOreFctr(randorename) * m_ore_fctr));
				else
					amount = (MyFixedPoint)Math.Round((decimal)(_event.largeOreAmt * getOreFctr(randorename) * m_ore_fctr));
				if (amount < (MyFixedPoint)0.01) amount = (MyFixedPoint)0.01;

				


				SandboxGameAssemblyWrapper.Instance.GameAction(() => {
					MyPhysicalInventoryItem i = new MyPhysicalInventoryItem(amount, MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(randorename));
					var meteorBuilder = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Meteor>();
					meteorBuilder.Item = i.GetObjectBuilder();
					meteorBuilder.PersistentFlags |= MyPersistentEntityFlags2.Enabled | MyPersistentEntityFlags2.InScene;
					var meteorEntity = MyEntities.CreateFromObjectBuilder(meteorBuilder);

					meteorEntity.WorldMatrix = Matrix.CreateWorld(spawnpos, forward, up);
					meteorEntity.Physics.LinearVelocity = vel;
					meteorEntity.Physics.AngularVelocity = MyUtils.GetRandomVector3Normalized() * MyUtils.GetRandomFloat(1.5f, 3);
					MyEntities.Add(meteorEntity);
					Sandbox.Game.Multiplayer.MySyncCreate.SendEntityCreated(meteorEntity.GetObjectBuilder());			
				} );
	
			}
			else
			{
				MyFixedPoint amount = 0;

				m_ore_fctr = m_gen.NextDouble();

				string randorename = getRandomOre();

				if (!large)
					amount = (MyFixedPoint)Math.Round((decimal)(_event.oreAmt * getOreFctr(randorename) * m_ore_fctr));
				else
					amount = (MyFixedPoint)Math.Round((decimal)(_event.largeOreAmt * getOreFctr(randorename) * m_ore_fctr));
				if (amount < (MyFixedPoint)0.01) amount = (MyFixedPoint)0.01;
				
				SandboxGameAssemblyWrapper.Instance.GameAction(() =>
				{
					MyPhysicalInventoryItem i = new MyPhysicalInventoryItem(amount, MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(randorename));
					//MyEntity obj = Sandbox.Game.Entities.MyEntities.CreateFromObjectBuilderAndAdd(meteorEntity.GetObjectBuilder());
					var meteorBuilder = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_FloatingObject>();
					meteorBuilder.Item = i.GetObjectBuilder();
					meteorBuilder.PersistentFlags |= MyPersistentEntityFlags2.Enabled | MyPersistentEntityFlags2.InScene;
					var meteorEntity = MyEntities.CreateFromObjectBuilder(meteorBuilder);
					meteorEntity.WorldMatrix = Matrix.CreateWorld(spawnpos, forward, up);
					meteorEntity.Physics.LinearVelocity = vel;
					meteorEntity.Physics.AngularVelocity = MyUtils.GetRandomVector3Normalized() * MyUtils.GetRandomFloat(1.5f, 3);
					MyEntities.Add(meteorEntity);
					Sandbox.Game.Multiplayer.MySyncCreate.SendEntityCreated(meteorEntity.GetObjectBuilder());
				});
			}


		}
		private void spawnMeteor(Vector3D spawnpos, Vector3D vel, PhysicsMeteroidEvents _event, bool large = false)
		{
			//insert keen code :)
			Vector3 forward = -MySector.DirectionToSunNormalized;
			Vector3 up = MyUtils.GetRandomVector3Normalized();
			while (forward == up)
				up = MyUtils.GetRandomVector3Normalized();

			Vector3 right = Vector3.Cross(forward, up);
			up = Vector3.Cross(right, forward);
			//end
			spawnMeteor(spawnpos, vel, forward, up, _event, large);
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
							int connectedPlayers = ServerNetworkManager.Instance.GetConnectedPlayers().Count;
							if (_event.enabled && meteoron && connectedPlayers > 0)
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
										createSectorStorm(_event);
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
					Log.Warn(ex);
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
					Log.Warn(ex.ToString());
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
			Log.Info("Shutting Down Physics Meteoroid Plugin.");
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

		#endregion
		#region "chatCommands"
		public void OnChatReceived(ChatManager.ChatEvent obj)
		{
			if (obj.Message[0] != '/')
				return;

			HandleChatMessage(obj);
		}
		public void OnChatSent(ChatManager.ChatEvent obj)
		{
			return;
		}
		public void HandleChatMessage(ChatManager.ChatEvent _event)
		{
			
			string[] words = _event.Message.Split(' ');
			words[0] = words[0].ToLower();
			bool isadmin = (PlayerManager.Instance.IsUserAdmin(_event.SourceUserId) || _event.SourceUserId == 0);
			if (words[0] == "/pm-save" && isadmin)
				CommandSaveXML(_event);
			if (words[0] == "/pm-load" && isadmin)
				CommandLoadXML(_event);
			if (words[0] == "/pm-loaddefaults" && isadmin)
				CommandLoadDefaults(_event);
			if (words[0] == "/pm-enable" && isadmin)
				CommandEnableMeteroid(_event);
			if (words[0] == "/pm-disable" && isadmin)
				CommandDisableMeteroid(_event);
			if (words[0] == "/pm-spawnwave" && isadmin)
				CommandSpawnWave(_event);
			if (words[0] == "/pm-sectorwave" && isadmin)
				CommandSpawnSectorWave(_event);
			if (words[0] == "/pm-sectorpos" && isadmin)
				CommandSpawnPos(_event);
			if (words[0] == "/pm-event" && isadmin)
				CommandEvent(_event);
			if (words[0] == "/pm-smite" && isadmin)
				CommandSmitePlayer(_event);
		}
		
		public void CommandEvent(ChatManager.ChatEvent _event)
		{
			try
			{
				string[] words = _event.Message.Split(' ');
				if(words.Count() >= 1)
				{
					if (words[1].ToLower() == "set")
					{
						if(words.Count() >= 2)
						{
							string eventname = words[2];
							PhysicsMeteroidEvents evt = events.Find(x => x.name.ToLower() == eventname.ToLower());
							if(evt != null)
							{
								String rem = String.Join(" ", words, 4, words.Count() - 4);
								if (words[3].ToLower() == "oreamt")
								{
									evt.oreAmt = Convert.ToDouble(rem.Trim());
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Ore amount set to " + evt.oreAmt.ToString());
								}
								else if (words[3].ToLower() == "largeoreamt")
								{
									evt.largeOreAmt = Convert.ToDouble(rem.Trim());
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Large Ore amount set to " + evt.largeOreAmt.ToString());
								}
								else if (words[3].ToLower() == "interval")
								{
									evt.interval = Convert.ToInt32(rem.Trim());
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Interval set to " + evt.interval.ToString());
								}
								else if (words[3].ToLower() == "randinterval")
								{
									evt.randInterval = Convert.ToInt32(rem.Trim());
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Random Interval set to " + evt.randInterval.ToString());
								}
								else if (words[3].ToLower() == "type")
								{
									if(words[4].ToLower() == "sector")
									{
										evt.eventType = "Sector";
										ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Type set to " + evt.eventType.ToString());
									}
									else if (words[4].ToLower() == "individual")
									{
										evt.eventType = "Individual";
										ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Type set to " + evt.eventType.ToString());

									}
									else if (words[4].ToLower() == "location")
									{
										evt.eventType = "Location";
										ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Type set to " + evt.eventType.ToString());

									}
									else
										ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Invalid type, valid types are Sector, Individual, and Location");

								}
								else if (words[3].ToLower() == "location")
								{
									int x = Convert.ToInt32(words[3]);
									int y = Convert.ToInt32(words[4]);
									int z = Convert.ToInt32(words[5]);
									Vector3D location = new Vector3D(x,y,z);
									evt.location = location;
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Location set to " + evt.location.ToString());
								}
								else
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Invalid arguement, valid arguments are oreamt, largeoreamt, interval, randinterval, type, location.");

							}
							else
								ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Could not find event: " + eventname.ToString());
						}
						else
						{
							ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Must specify eventname /pm-event set [eventname] [args]");
						}
					}
					else if (words[1].ToLower() == "get")
					{
						if (words.Count() >= 2)
						{
							string eventname = words[2];
							PhysicsMeteroidEvents evt = events.Find(x => x.name.ToLower() == eventname.ToLower());
							if(evt != null)
							{
								if (words[3].ToLower() == "oreamt")
								{
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Ore amount is " + evt.oreAmt.ToString());
								}
								else if (words[3].ToLower() == "largeoreamt")
								{
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Large Ore amount is " + evt.largeOreAmt.ToString());
								}
								else if (words[3].ToLower() == "interval")
								{
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Interval is " + evt.interval.ToString());
								}
								else if (words[3].ToLower() == "randinterval")
								{
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Random Interval is " + evt.randInterval.ToString());
								}
								else if (words[3].ToLower() == "type")
								{
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Type is " + evt.eventType.ToString());
								}
								else if (words[3].ToLower() == "location")
								{

									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Location is " + evt.location.ToString());
								}
								else
									ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Invalid arguement, valid arguments are oreamt, largeoreamt, interval, randinterval, type, location.");

							}
							else
								ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Could not find event: " + eventname.ToString());
						}
						else
						{
							ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Must specify eventname /pm-event get [eventname] [args]");
						}
					}
					else if (words[1].ToLower() == "create")
					{
						if (words.Count() >= 2)
						{
							string eventname = words[2];
							PhysicsMeteroidEvents evt = events.Find(x => x.name.ToLower() == eventname.ToLower());
							if(evt == null)
							{
								PhysicsMeteroidEvents newevent = new PhysicsMeteroidEvents();
								newevent.name = eventname;
								events.Add(newevent);
							}
						}
						else
						{
							ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Must specify eventname /pm-event create [eventname]");
						}
					}
					else if (words[1].ToLower() == "delete")
					{
						if (words.Count() >= 2)
						{
							string eventname = words[2];
							PhysicsMeteroidEvents evt = events.Find(x => x.name.ToLower() == eventname.ToLower());
							if(evt == null)
							{
								ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Could not find event: " + eventname.ToString());
							}
							else
							{
								events.Remove(evt);
								ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Deleted " + eventname.ToString());
							}
						}
						else
						{
							ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Must specify eventname /pm-event delete [eventname]");
						}
					}
					else
					{
						ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Invalid arguement, second arguement must be set, get, create, or delete");
					}
				}
			}
			catch (Exception)
			{

			}
		}

		public void CommandSmitePlayer(ChatManager.ChatEvent _event)
		{
			try
			{
				string[] words = _event.Message.Split(' ');
				if (words.Count() > 2)
				{
					String rem = String.Join(" ", words, 2, words.Count() - 2);
					if (words[1] == "small")
					{
						smitePlayer(rem, false);
						ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Dropping small rock on: " + rem);
					}
					if (words[1] == "large")
					{
						smitePlayer(rem, true);
						ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Dropping large rock on: " + rem);
					}
				}
				if (words.Count() > 1)
				{
					String rem = String.Join(" ", words, 1, words.Count() - 1);
					smitePlayer(rem, false);
					ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Dropping small rock on: " + rem);
				}
			}
			catch (PMNoTargetException ex)
			{
				//ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Could not target player: " + ex.Message.ToString());
				debugWrite(ex.ToString());
			}
		}

		public void CommandSpawnPos(ChatManager.ChatEvent _event)
		{
			string[] words = _event.Message.Split(' ');
			if (words.Count() > 3)
			{
				int x = Convert.ToInt32(words[1]);
				int y = Convert.ToInt32(words[2]);
				int z = Convert.ToInt32(words[3]);
				Thread t = new Thread(() => showerPosition(new Vector3D(x, y, z)));
				t.Start();
				ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Starting meteoriod storm at " + x.ToString() + " " + y.ToString() + " " + z.ToString());
			}
		}

		public void CommandSpawnWave(ChatManager.ChatEvent _event)
		{
			Thread t = new Thread(() => createMeteorStorm(events.First()));
			t.Start();
			ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Starting meteoriod storm");
			return;
		}

		public void CommandSpawnSectorWave(ChatManager.ChatEvent _event)
		{
			Thread t = new Thread(() => createSectorStorm(events.First()));
			t.Start();
			ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Starting meteoriod sector wide storm");
			return;
		}

		public void CommandEnableMeteroid(ChatManager.ChatEvent _event)
		{
			ChatManager.Instance.SendPrivateChatMessage(_event.RemoteUserId, "Automatic Meteoroid storms enabled.");
			settings.meteorOn = true;
		}

		public void CommandDisableMeteroid(ChatManager.ChatEvent _event)
		{
			ChatManager.Instance.SendPrivateChatMessage(_event.SourceUserId, "Automatic Meteoroid storms disabled.");
			settings.meteorOn = false;
		}

		public void CommandSaveXML(ChatManager.ChatEvent _event)
		{
			saveXML();
			try
			{
				ChatManager.Instance.SendPrivateChatMessage(_event.RemoteUserId, "Physics Meteroid Settings Saved.");
			}
			catch
			{

			}
		}

		public void CommandLoadXML(ChatManager.ChatEvent _event)
		{
			loadXML(false);
			try
			{
				ChatManager.Instance.SendPrivateChatMessage(_event.RemoteUserId, "Physics Meteroid Settings Loaded.");
			}
			catch
			{

			}
		}

		public void CommandLoadDefaults(ChatManager.ChatEvent _event)
		{
			loadXML(true);
			try
			{
				ChatManager.Instance.SendPrivateChatMessage(_event.RemoteUserId, "Physics Meteroid Settings Defaults Loaded.");
			}
			catch
			{

			}
		}
		
		#endregion

		#endregion
	}
}
