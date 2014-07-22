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
	public class PhysicsMeteroidCore : PluginBase, IChatEventHandler
	{
		
		#region "Attributes"
		[field: NonSerialized()]
		private double m_ore_amt = 60000;
		[field: NonSerialized()]
		private double m_ore_fctr = 1;
		[field: NonSerialized()]
		private float m_maxVelocityFctr = 1F;
		[field: NonSerialized()]
		private float m_velocityFctr = 3F;
		[field: NonSerialized()]
		private bool m_running = false;
		[field: NonSerialized()]
		private bool m_meteoron = true;

		[field: NonSerialized()]
		private static Type m_InventoryItemType = new MyObjectBuilderType(typeof(MyObjectBuilder_InventoryItem));
		[field: NonSerialized()]
		private static Type m_OreType = new MyObjectBuilderType(typeof(MyObjectBuilder_Ore));
		[field: NonSerialized()]
		private static Type m_FloatingObjectType = new MyObjectBuilderType(typeof(MyObjectBuilder_FloatingObject));
		
		[field: NonSerialized()]
		private Random m_gen;

		#endregion

		#region "Constructors and Initializers"

		public void Core()
		{
			Console.WriteLine("PhysicsMeteoroidPlugin '" + Id.ToString() + "' constructed!");	
		}

		public override void Init()
		{
			m_running = false;
			m_gen = new Random(3425325);//temp hash
			m_maxVelocityFctr = 1F;
			m_velocityFctr = 3F;
			m_ore_amt = 60000;
			Console.WriteLine("PhysicsMeteoroidPlugin '" + Id.ToString() + "' initialized!");
			loadXML();
		}

		#endregion

		#region "Properties"

		[Category("Physics Meteoriod Plugin")]
		[Description("Maximum meteoriod size.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public double ore_amt
		{
			get { return m_ore_amt; }
			set { if (value > 0) m_ore_amt = value; }
		}
		
		[Browsable(true)]
		[ReadOnly(true)]
		public string Location
		{
			get { return System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\"; }
		
		}
		[Category("Physics Meteoriod Plugin")]
		[Description("Set Maximum velocity (does not work yet)")]
		[Browsable(true)]
		[ReadOnly(false)]
		
		public float maxVelocityFctr
		{
			get { return m_maxVelocityFctr; }
			set { m_maxVelocityFctr = value; }
		}
		[Category("Physics Meteoriod Plugin")]
		[Description("Multiply original velocity by this factor.")]
		[Browsable(true)]
		[ReadOnly(false)]
		
		public float velocityFctr
		{
			get { return m_velocityFctr; }
			set { m_velocityFctr = value; }
		}

		[Category("Physics Meteoriod Plugin")]
		[Description("Enables or Disables meteoriod swarms")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool meteoron
		{
			get { return m_meteoron; }
			set { if (value) m_meteoron = true; else m_meteoron = false; }
		}
		#endregion

		#region "Methods"

		public void saveXML()
		{

			XmlSerializer x = new XmlSerializer(typeof(PhysicsMeteroidCore));
			TextWriter writer = new StreamWriter(Location + "Configuration.xml");
			x.Serialize(writer, this);
			writer.Close();

		}
		public void loadXML(bool l_default)
		{
			try
			{
				if (File.Exists(Location + "Configuration.xml"))
				{
					XmlSerializer x = new XmlSerializer(typeof(PhysicsMeteroidCore));
					TextReader reader = new StreamReader(Location + "Configuration.xml");
					PhysicsMeteroidCore obj = (PhysicsMeteroidCore)x.Deserialize(reader);
					meteoron = obj.meteoron;
					velocityFctr = obj.velocityFctr;
					maxVelocityFctr = obj.maxVelocityFctr;
					ore_amt = obj.ore_amt;
					reader.Close();
				}
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole("Could not load configuration:" + ex.ToString());
			}

		}
		public void loadXML()
		{
			loadXML(false);
		}
		public void velocityloop(FloatingObject obj, Vector3Wrapper vel)
		{
			Thread.Sleep(10);
			for (int count = 20; count > 0; count--)
			{
				if (obj.Mass > 0)
				{
					obj.MaxLinearVelocity = m_maxVelocityFctr;
					obj.LinearVelocity = vel;
					if (SandboxGameAssemblyWrapper.IsDebugging)
					{
						LogManager.APILog.WriteLineAndConsole("Meteor entityID: " + obj.EntityId.ToString() + " Velocity: " + vel.ToString());
					}
					break;
				}
				Thread.Sleep(10);
			}
			
			return;
		}
		private void CreateMeteorStorm()
		{
			try
			{
				int minmeteor = 1;
				int maxmeteor = 50;
				int ranmeteor = m_gen.Next(maxmeteor - minmeteor) + minmeteor;
				
				CubeGridEntity target = FindTarget();
				Vector3Wrapper pos = target.Position;
				Vector3Wrapper velnorm = Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1));
				Vector3Wrapper stormpos = Vector3.Add(pos, Vector3.Multiply(Vector3.Negate(velnorm), 3000));
				//spawn meteors in a random position around stormpos with the velocity of velnorm
				for (int i = 0; i < ranmeteor; i++)
				{
					Thread.Sleep(1000);
					spawnMeteor(
						Vector3.Add(
							stormpos, 
							Vector3.Multiply(
								Vector3.Normalize(
									new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)
									),
								20)
							),
						Vector3.Add(
							Vector3.Multiply(
								velnorm, 
								(float)(50d + m_gen.NextDouble() * 55d)
								),
							Vector3.Multiply(Vector3.Normalize(new Vector3Wrapper((float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1, (float)m_gen.NextDouble() * 2 - 1)), 0.2F)
							)
						);

				}
			}
			catch (PMNoPlayersException ex)
			{
				//do nothing
				Console.WriteLine("Meteor Shower Aborted: " + ex.ToString());
			}
			catch (PMNoTargetException ex)
			{
				Console.WriteLine("Meteor Shower Aborted: " + ex.ToString());
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole(ex.ToString());
			}
		}
		private CubeGridEntity FindTarget()
		{
			//pull online players
			List<CubeGridEntity> targets = new List<CubeGridEntity>();
			List<ulong> playerList = ServerNetworkManager.Instance.GetConnectedPlayers();
			//select a target
			if (playerList.Count() <= 0)
				throw new PMNoPlayersException("No players found");


			int targetno = m_gen.Next(playerList.Count());
			if (targetno > playerList.Count() || targetno < 0) throw new PMNoTargetException("Invalid Target");
			ulong utarget = playerList[targetno];

			//convert utarget to target, target is just an id. not supported in API yet


			long target = 0;//set to targetid obtained from utarget
			if (target > 0)
			{
				List<CubeGridEntity> list = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
				foreach (var item in list)
				{
					try
					{

						foreach (var cubeBlock in item.CubeBlocks)
						{
							if(cubeBlock.Owner == target) 
							{
								targets.Add(item);
								break;
							}
						}
						
					}
					catch (Exception ex)
					{
						LogManager.GameLog.WriteLine(ex);
					}
				}
				targetno = (int)Math.Floor( (double)targets.Count() * m_gen.NextDouble() );
				if (targetno > targets.Count()) throw new PMNoTargetException("Invalid Target");
				return targets[targetno];
			}
			else
				throw new PMNoTargetException("Invalid Target");
		}
		private void spawnMeteor(Vector3Wrapper spawnpos, Vector3Wrapper vel, Vector3Wrapper up, Vector3Wrapper forward)
		{
			MyObjectBuilder_FloatingObject tempobject;
			MyObjectBuilder_Ore tempore = new MyObjectBuilder_Ore();
			MyObjectBuilder_InventoryItem tempitem = new MyObjectBuilder_InventoryItem();
			tempore.SetDefaultProperties();
			FloatingObject physicsmeteor;
			m_ore_fctr = m_gen.NextDouble();

			tempitem = (MyObjectBuilder_InventoryItem)MyObjectBuilder_InventoryItem.CreateNewObject(m_InventoryItemType);
			tempitem.PhysicalContent = (MyObjectBuilder_PhysicalObject)MyObjectBuilder_PhysicalObject.CreateNewObject(m_OreType);
			tempitem.PhysicalContent.SubtypeName = getRandomOre();
			tempitem.AmountDecimal = Math.Round((decimal)(m_ore_amt * getOreFctr(tempitem.PhysicalContent.SubtypeName) * m_ore_fctr));
			tempitem.ItemId = 0;

			tempobject = (MyObjectBuilder_FloatingObject)MyObjectBuilder_FloatingObject.CreateNewObject(m_FloatingObjectType);
			tempobject.Item = tempitem;

			physicsmeteor = new FloatingObject(tempobject);
			physicsmeteor.EntityId = FloatingObject.GenerateEntityId();
			physicsmeteor.Up = up;
			physicsmeteor.Forward = forward;
			physicsmeteor.Position = spawnpos;
			physicsmeteor.LinearVelocity = vel;
			physicsmeteor.MaxLinearVelocity = 104.7F * m_maxVelocityFctr;
			if (SandboxGameAssemblyWrapper.IsDebugging)
			{
				LogManager.APILog.WriteLineAndConsole("Meteor entityID: " + physicsmeteor.EntityId.ToString() + " Velocity: " + vel.ToString());
			}
			SectorObjectManager.Instance.AddEntity(physicsmeteor);
			//workaround for the velocity problem.
			Thread physicsthread = new Thread(() => velocityloop(physicsmeteor, vel));
			physicsthread.Start();	

		}
		private void spawnMeteor(Vector3Wrapper spawnpos, Vector3Wrapper vel)
		{
			spawnMeteor(spawnpos, vel, new Vector3Wrapper(), new Vector3Wrapper());

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
			return "Platinum";
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
			}
			return 1;
		}
		
		#region "EventHandlers"

		public override void Update()
		{
			
			//prevent multiple update threads to run at once.
			if (m_running) return;
			m_running = true;
			Vector3Wrapper up;
			Vector3Wrapper forward;
			Vector3Wrapper pos;
			Vector3Wrapper velocity;
			
			List<ulong> connectedPlayers = ServerNetworkManager.Instance.GetConnectedPlayers();
			List<Meteor> entityList = SectorObjectManager.Instance.GetTypedInternalData<Meteor>();

			foreach (var sectorObject in entityList)
			{
				if (!sectorObject.IsDisposed)
				{
					up = sectorObject.Up;
					forward = sectorObject.Forward;
					pos = sectorObject.Position;
					velocity = sectorObject.LinearVelocity;
					if (SandboxGameAssemblyWrapper.IsDebugging)
					{
						LogManager.APILog.WriteLineAndConsole("Orig Velocity: " + velocity.ToString());
					}
					velocity = Vector3.Multiply(velocity, m_velocityFctr);
					if (SandboxGameAssemblyWrapper.IsDebugging)
					{
						LogManager.APILog.WriteLineAndConsole("Mult Velocity: " + velocity.ToString());
					}
					sectorObject.Dispose();
					if(!meteoron)
						continue;
					if (connectedPlayers.Count > 0 )
					{
						try
						{
							spawnMeteor(pos, velocity, up, forward);					
						}
						catch (Exception ex)
						{
							LogManager.APILog.WriteLineAndConsole(ex.ToString());
						}
							
					}
				}
			}
			m_running = false;
		}

		public override void Shutdown()
		{
			saveXML();
			m_running = true;
			Thread.Sleep(300);//wait for functions to complete. 
			m_running = false;
			m_maxVelocityFctr = 1F;
			m_velocityFctr = 3F;
			return;
		}

		public void OnChatReceived(SEModAPIExtensions.API.ChatManager.ChatEvent obj)
		{

			if (obj.sourceUserId == 0)
				return;
			bool isadmin = SandboxGameAssemblyWrapper.Instance.IsUserAdmin(obj.sourceUserId);

			if (obj.message[0] == '.')
			{

				string[] words = obj.message.Split(' ');
				string rem = "";
				//proccess
				if (words.Count() > 2 && isadmin)
				{
					rem = String.Join(" ", words, 2, words.Count() - 2);
					if (words[0] == ".set")
					{
						
						if (words[1] == "ore_amt")
						{
							ore_amt = Convert.ToDouble(rem.Trim());
						}
					}
				}


				if (isadmin && words[0] == ".pm-enable")
				{
					m_meteoron = true;
					return;
				}

				if (isadmin && words[0] == ".pm-disable")
				{
					m_meteoron = false;
					return;
				}

				if (isadmin && words[0] == ".pm-save")
				{
					saveXML();
					return;
				}
				if (isadmin && words[0] == ".pm-loaddefault")
				{
					loadXML(true);
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
