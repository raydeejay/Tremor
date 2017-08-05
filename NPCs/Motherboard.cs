using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Tremor.Items;

// TODO: fix Motherboard despawn on first hit
// TODO: motherboard does not spawn in MP
// TODO: rewrite this thing, lol

namespace Tremor.NPCs
{
	// has two stages
	// some other bosses do
	// the possibility to factor that exists
	// each stage has different appearing, disappearing, and following times

	public class Stage
	{
		public int followPlayerTime; // Time of following player in 1st stage
		public int disappearingTime; // Time of disappearing in 1st stage
		public int appearingTime; // Time of appearing in 1st stage
		public int stateTime; // Stage time
		public int appearTime;

		public int GetStateTime => appearingTime + disappearingTime + followPlayerTime;

		protected const int AnimationRate = 6; // Animation rate
		protected int _currentFrame; // Current frame
		protected int _timeToAnimation = 6; // Animation rate

		protected const int LaserYOffset = 95; // Laser spawn offset by Y value
		protected const int LaserDamage = 40; // Laser damage
		protected const float LaserKb = 1; // Laser knockback

		protected const int SecondShootCount = 3;
		protected const float SecondShootSpeed = 15f;
		protected const int SecondShootDamage = 30;
		protected const float SecondShootKn = 1.0f;
		protected const int SecondShootRate = 60;
		protected const int SecondShootSpread = 65;
		protected const float SecondShootSpreadMult = 0.05f;

		protected int _secondShootTime = 60;

		public Stage(int followPlayerTime, int disappearingTime, int appearingTime)
		{
			this.followPlayerTime = followPlayerTime;
			this.disappearingTime = disappearingTime;
			this.appearingTime = appearingTime;
			this.stateTime = appearingTime + disappearingTime + followPlayerTime;
		}

		public virtual void AI(Motherboard motherboard) {}
		public virtual void Animate(Motherboard boss) {}
		public virtual void AdjustHead(Motherboard boss) {}
		public virtual void Start(Motherboard boss) {}

		protected void SecondShoot(Motherboard boss)
		{
			if (!boss.isInsideTerrain()) {
				--_secondShootTime;
			}

			if (_secondShootTime <= 0)
			{
				_secondShootTime = SecondShootRate;
				for (int i = 0; i < 2; i++) {
					Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y + 95, 0, 0, boss.mod.ProjectileType("projMotherboardSuperLaser"), SecondShootDamage, SecondShootKn, 0, boss.npc.whoAmI, i);
				}
			}
		}
	}

	// Phase 1

	// In the first phase after she spawns, she will also spawn in with
	// several Signal Drones. The drones will fly around her and will
	// occasionally charge at the player. During this time she will chase
	// after the player slowly and is completely immune to damage. After
	// the drones pass beams between each other, she will fire 3 shadow
	// laser toward the player. Every few seconds after destroying some of
	// the drones, she will spawn new ones. The phase ends when all of the
	// drones are destroyed.

	public class Stage1 : Stage
	{
		private const int MaxDrones = 20; // Maximum amount of Drones
		private const int DronSpawnAreaX = 300; // Area size in which Drone can spawn by X value
		private const int DronSpawnAreaY = 300; // Area size in which Drone can spawn by Y value
		private const int StartDronCount = 8; // Initial amount of Drones

		private const int ShootRate = 150; // Fire rate in ticks
		private const int TimeToLaserRate = 3; // Fire rate (From drones to player)
		private const int LaserType = ProjectileID.ShadowBeamHostile; // Laser type

		private bool _shootNow; // Does the Motherboard shoots right now?
		private int _timeToNextDrone = 1; // Time for spawning next Drone
		private int _timeToShoot = 60; // Time for next shoot
		private int _timeToLaser = 3; // Time for next shoot (Drones lasers)

		private List<int> _signalDrones = new List<int>(); // ID of Signal Drones
		private int _lastSignalDron = -1; // Last Drone

		private int GetTimeToNextDrone => (Main.rand.Next(3, 6) * 60);

		public Stage1(int followPlayerTime, int disappearingTime, int appearingTime) : base(followPlayerTime, disappearingTime, appearingTime) {}

		public override void Start(Motherboard boss)
		{
			for (int i = 0; i < StartDronCount; i++)
			{
				Vector2 spawnPosition = Helper.RandomPointInArea(new Vector2(boss.npc.Center.X - DronSpawnAreaX / 2, boss.npc.Center.Y - DronSpawnAreaY / 2), new Vector2(boss.npc.Center.X + DronSpawnAreaX / 2, boss.npc.Center.Y + DronSpawnAreaY / 2));
				_signalDrones.Add(NPC.NewNPC((int)spawnPosition.X, (int)spawnPosition.Y, boss.mod.NPCType("SignalDron"), 0, 0, 0, 0, boss.npc.whoAmI));
			}
		}

		public override void Animate(Motherboard boss)
		{
			if (--_timeToAnimation <= 0)
			{

				if (++_currentFrame > 3)
					_currentFrame = 1;
				_timeToAnimation = AnimationRate;
				boss.npc.frame = boss.GetFrame(_currentFrame);
			}
		}

		public override void AdjustHead(Motherboard boss)
		{
			Main.npcHeadBossTexture[boss.headTexture] = boss.mod.GetTexture("NPCs/Motherboard_Head_Boss");
		}

		private void CheckDrones(Motherboard boss) // Removes all dead Drones from the list
		{
			// Passing through each element of array with ID of clampers
			for (int index = 0; index < _signalDrones.Count; index++)
			{
				// If NPC with ID from array isn't a Drone or is dead then... FIX THIS AWFULNESS!!!
				if (!Main.npc[_signalDrones[index]].active || Main.npc[_signalDrones[index]].type != boss.mod.NPCType("SignalDron"))
				{
					_signalDrones.RemoveAt(index); // Remove ID of this NPC from Drones list
					--index; // Lowering index by 1 in order not to miss 1 element in array of IDs
				}
			}
		}

		private void SpawnDrones(Motherboard boss) // If it is time to spawn a Drone
		{
			// If the current amount of Drones = or > maximum amount of drones then...
			if (_signalDrones.Count >= MaxDrones)
			{
				return;
			}

			// Lowering the time of spawning next Drone. If the time < or = 0 then...
			if (--_timeToNextDrone <= 0)
			{
				// Setting new time of spawning Drones
				_timeToNextDrone = GetTimeToNextDrone;

				// Defining random position around the boss (Via Helper) and write it into Var 01
				Vector2 spawnPosition = Helper.RandomPointInArea(new Vector2(boss.npc.Center.X - DronSpawnAreaX / 2, boss.npc.Center.Y - DronSpawnAreaY / 2), new Vector2(boss.npc.Center.X + DronSpawnAreaX / 2, boss.npc.Center.Y + DronSpawnAreaY / 2));

				// Spawning Drone with coordinates from Var 01 and with ID in ai[3]
				_signalDrones.Add(NPC.NewNPC((int)spawnPosition.X, (int)spawnPosition.Y + LaserYOffset, boss.mod.NPCType("SignalDron"), 0, 0, 0, 0, boss.npc.whoAmI));
			}
		}

		private void ShootDrones(Motherboard boss) // If it is time to shoot
		{
			if (_signalDrones.Count <= 0) // If there're no Drones then...
				return; // Ending the method

			// If it is time to shoot or if the boss is already shooting then...
			if (--_timeToShoot <= 0 || _shootNow)
			{
				if (_lastSignalDron == -1 && boss.npc.ai[0] != -1)
					return;

				 // Setting new shoot time
				_timeToShoot = ShootRate;

				// Shooting
				_shootNow = true;

				 // If it is time to shoot Drones lasers then...
				if (--_timeToLaser <= 0)
				{
					// Set new shoot time
					_timeToLaser = TimeToLaserRate;

					// If there's no last Drone shooting then...
					if (_lastSignalDron == -1)
					{
						// Take new Drone from the array
						_lastSignalDron = 0;

						// Shoot the Drone from the boss
						Main.projectile[Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y, 0, 0, boss.mod.ProjectileType("projMotherboardLaser"), LaserDamage, LaserKb, 0, boss.npc.whoAmI, _signalDrones[_lastSignalDron])].localAI[1] = 1;

						return;
					}

					// Taking new Drone
					++_lastSignalDron;

					// Checking for exiting the bounds of array
					if (_lastSignalDron < _signalDrones.Count)
					{						// Shoot laser
						Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y, 0, 0, boss.mod.ProjectileType("projMotherboardLaser"), LaserDamage, LaserKb, 0, _signalDrones[_lastSignalDron - 1], _signalDrones[_lastSignalDron]);
					}

					// If it is last drone then...
					if (_lastSignalDron + 1 >= _signalDrones.Count)
					{
						Vector2 vel = Helper.VelocityToPoint(Main.npc[_signalDrones[_signalDrones.Count - 1]].Center, Main.player[boss.npc.target].Center, 15f);

						for (int i = 0; i < SecondShootCount; i++)
						{
							Vector2 velocity = Helper.VelocityToPoint(Main.npc[_signalDrones[_signalDrones.Count - 1]].Center, Main.player[boss.npc.target].Center, SecondShootSpeed);
							velocity.X = velocity.X + Main.rand.Next(-SecondShootSpread, SecondShootSpread + 1) * SecondShootSpreadMult;
							velocity.Y = velocity.Y + Main.rand.Next(-SecondShootSpread, SecondShootSpread + 1) * SecondShootSpreadMult;
							Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y, velocity.X, velocity.Y, LaserType, SecondShootDamage, SecondShootKn);
						}

						// Shooting the player with another laser, setting last Drone to -1 and ending the cycle of shooting
						_lastSignalDron = -1;
						_shootNow = false;
					}
				}
			}
		}

		private void MaybeChangeStage(Motherboard boss) // Trying change stage
		{
			CheckDrones(boss); // Checking for Drones
			if (_signalDrones.Count <= 0) // If there are no Drones alive
			{
				boss.stage = boss.stage2; // Toggling off 1st Stage
				boss.stage.Start(boss);
			}
		}

		public override void AI(Motherboard boss)
		{
			CheckDrones(boss); // Removes dead Drones from the list
			SpawnDrones(boss); // Spawns Drones
			ShootDrones(boss); // Shoots lasers
			MaybeChangeStage(boss);
		}
	}

	// Phase 2

	// Once all of the drones are destroyed, she will be vulnerable to
	// attacks. She will replace the drones with 4 new minions called
	// Clampers, which will be attached to her. They will chase after the
	// player while Motherboard moves aimlessly around you and
	// teleporting. After about 80% of her health is gone she will detach
	// the Clampers and begin to aggressively chase you.

	public class Stage2 : Stage
	{
		private List<int> _clampers = new List<int>(); // Clampers list

		public override void Animate(Motherboard boss)
		{
			if (--_timeToAnimation <= 0)
			{
				if (++_currentFrame > 3)
					_currentFrame = 1;
				_timeToAnimation = AnimationRate;
				boss.npc.frame = boss.GetFrame(_currentFrame + 3);
			}
		}

		public override void AdjustHead(Motherboard boss)
		{
			Main.npcHeadBossTexture[boss.headTexture] = boss.mod.GetTexture("NPCs/Motherboard_Head_Boss");
		}

		public Stage2(int followPlayerTime, int disappearingTime, int appearingTime) : base(followPlayerTime, disappearingTime, appearingTime) {}

		public override void Start(Motherboard boss)
		{
			_clampers = new List<int>
			{
				NPC.NewNPC((int) boss.npc.Center.X - 15, (int) boss.npc.Center.Y + 25, boss.mod.NPCType("Clamper"), 0, 0, 0, 0, boss.npc.whoAmI),
				NPC.NewNPC((int) boss.npc.Center.X - 10, (int) boss.npc.Center.Y + 25, boss.mod.NPCType("Clamper"), 0, 0, 0, 0, boss.npc.whoAmI),
				NPC.NewNPC((int) boss.npc.Center.X + 10, (int) boss.npc.Center.Y + 25, boss.mod.NPCType("Clamper"), 0, 0, 0, 0, boss.npc.whoAmI),
				NPC.NewNPC((int) boss.npc.Center.X + 15, (int) boss.npc.Center.Y + 25, boss.mod.NPCType("Clamper"), 0, 0, 0, 0, boss.npc.whoAmI)
			};

			for (int i = 0; i <= 3; i++)
			{
				Main.npc[_clampers[i]].localAI[1] = i + 1;
			}

			boss.npc.dontTakeDamage = false;
		}

		private void CheckClampers(Motherboard boss)
		{
			for (int index = 0; index < _clampers.Count; index++) // Passing through each element of array with ID of clampers
				if (!Main.npc[_clampers[index]].active || Main.npc[_clampers[index]].type != boss.mod.NPCType("Clamper")) // If
																													 // NPC with ID from array isn't a Clamper or is dead then...
				{
					_clampers.RemoveAt(index); // Remove ID of this NPC from Clamper list
					--index; // Lowering index by 1 in order not to miss 1 element in array of IDs
				}
			foreach (int ID in _clampers)
			{
				int id = Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y + LaserYOffset, 0, 0, boss.mod.ProjectileType("projClamperLaser"), LaserDamage, LaserKb, 0, boss.npc.whoAmI, ID);
				Main.projectile[id].localAI[1] = stateTime;
			}
		}

		// phase1
		// phase2
		// phase3

		public override void AI(Motherboard boss)
		{
			boss.Teleport();
			boss.npc.TargetClosest(true);

			// following
			if (boss.npc.ai[1] == 0f)
			{
				// runs only SP/server side
				if (Main.netMode != 1)
				{
					// increment the something timer
					boss.npc.localAI[1] += 1f;

					// if the timer is due
					if (boss.npc.localAI[1] >= 120 + Main.rand.Next(200))
					{
						boss.npc.localAI[1] = 0f;
						boss.npc.TargetClosest(true);

						// attempt to find coords somewhere around the target (max 100 tries)
						bool foundCoords = false;
						int attempts = 0;
						int coordX = 0;
						int coordY = 0;

						while (true && attempts < 100)
						{
							attempts++;
							coordX = (int) Main.player[boss.npc.target].Center.X / 16 + Main.rand.Next(-50, 51);
							coordY = (int) Main.player[boss.npc.target].Center.Y / 16 + Main.rand.Next(-50, 51);

							// break as soon as we find a shootable pair of coordinates
							if (!WorldGen.SolidTile(coordX, coordY)
							    && Collision.CanHit(new Vector2(coordX * 16, coordY * 16), 1, 1,
										Main.player[boss.npc.target].position,
										Main.player[boss.npc.target].width,
										Main.player[boss.npc.target].height))
							{
								foundCoords = true;
								break;
							}
						}

						// set new coords if found
						if (foundCoords)
						{
							boss.npc.ai[1] = 1f;
							boss.npc.ai[2] = coordX;
							boss.npc.ai[3] = coordY;
							boss.npc.netUpdate = true;
						}
						return;
					}
				}
			}
			// appearing
			else if (boss.npc.ai[1] == 1f)
			{
				boss.npc.alpha += 3;
				if (boss.npc.alpha >= 255)
				{
					boss.npc.alpha = 255;
					boss.npc.position.X = boss.npc.ai[2] * 16f - boss.npc.width / 2;
					boss.npc.position.Y = boss.npc.ai[3] * 16f - boss.npc.height / 2;
					boss.npc.ai[1] = 2f;
					return;
				}
			}
			// appearing
			else if (boss.npc.ai[1] == 2f)
			{
				boss.npc.alpha -= 3;
				if (boss.npc.alpha <= 0)
				{
					boss.npc.alpha = 0;
					boss.npc.ai[1] = 0f;
					return;
				}
			}

			CheckClampers(boss);
			SecondShoot(boss);
			return;
		}
	}

	// BOSS CODE

	[AutoloadBossHead]
	public class Motherboard : ModNPC
	{
		public Stage stage0 = new Stage(120, 30, 30);
		public Stage stage1 = new Stage1(120, 30, 30);
		public Stage stage2 = new Stage2(90, 30, 30);

		public Stage stage;

		public int headTexture = 0;

		// private int _stateTime = StateOneAppearingTime + StateOneDisappearingTime + StateOneFollowPlayerTime; // Stage time

		// private int GetStateTime => GetAppearingTimeNow + GetDisappearingTimeNow + GetFollowPlayerTimeNow;

		// private int GetFollowPlayerTimeNow => (stage == stage1) ? stage1.followPlayerTime : stage2.followPlayerTime;
		private int GetDisappearingTimeNow => (stage == stage1) ? stage1.disappearingTime : stage2.disappearingTime;
		private int GetAppearingTimeNow => (stage == stage1) ? stage1.appearingTime : stage2.appearingTime;

		// public override bool UsesPartyHat() => false;

		public override void SetStaticDefaults()
		{
			DisplayName.SetDefault("Motherboard");
			Main.npcFrameCount[npc.type] = 6;

			NPCID.Sets.MustAlwaysDraw[npc.type] = true;
			NPCID.Sets.NeedsExpertScaling[npc.type] = true;
		}

		public override void SetDefaults()
		{
			npc.lifeMax = 45000;
			npc.damage = 30;
			npc.knockBackResist = 0f;
			npc.defense = 70;
			npc.width = 170;
			npc.height = 160;
			npc.aiStyle = 2;
			npc.npcSlots = 50f;
			music = MusicID.Boss3;

			npc.dontTakeDamage = true;
			npc.noTileCollide = true;
			npc.noGravity = true;
			npc.boss = true;
			npc.lavaImmune = true;

			npc.HitSound = SoundID.NPCHit4;
			npc.DeathSound = SoundID.NPCDeath10;

			bossBag = mod.ItemType<MotherboardBag>();
			headTexture = NPCID.Sets.BossHeadTextures[npc.type];

			stage = stage0;
		}

		public override void ScaleExpertStats(int numPlayers, float bossLifeScale)
		{
			npc.lifeMax = (int)(npc.lifeMax * 0.625f * bossLifeScale);
			npc.damage = (int)(npc.damage * 0.6f);
		}

		public override void HitEffect(int hitDirection, double damage)
		{
			if (npc.life <= 0)
			{
				for (int k = 0; k < 20; k++)
				{
					Dust.NewDust(npc.position, npc.width, npc.height, 151, 2.5f * hitDirection, -2.5f, 0, default(Color), 0.7f);
				}

				Gore.NewGore(npc.position, npc.velocity, mod.GetGoreSlot("Gores/MotherboardGore1"), 1f);
				Gore.NewGore(npc.position, npc.velocity, mod.GetGoreSlot("Gores/MotherboardGore2"), 1f);
				Gore.NewGore(npc.position, npc.velocity, mod.GetGoreSlot("Gores/MotherboardGore2"), 1f);
				Gore.NewGore(npc.position, npc.velocity, mod.GetGoreSlot("Gores/MotherboardGore3"), 1f);
				Gore.NewGore(npc.position, npc.velocity, mod.GetGoreSlot("Gores/MotherboardGore4"), 1f);
			}
		}

		public override void NPCLoot()
		{
			NPC.downedMechBossAny = true;
			NPC.downedMechBoss1 = true;
			TremorWorld.downedBoss[TremorWorld.Boss.Motherboard] = true;

			if (Main.expertMode)
			{
				npc.DropBossBags();
			}
			else
			{
				if (Main.rand.NextBool())
				{
					this.SpawnItem((short)mod.ItemType<SoulofMind>(), Main.rand.Next(20, 40));
				}
				if (Main.rand.NextBool())
				{
					this.SpawnItem(ItemID.GreaterHealingPotion, Main.rand.Next(5, 15));
				}
				if (Main.rand.NextBool())
				{
					this.SpawnItem(ItemID.HallowedBar, Main.rand.Next(15, 35));
				}
				if (Main.rand.Next(7) == 0)
				{
					this.SpawnItem((short)mod.ItemType<MotherboardMask>());
				}
			}

			if (Main.rand.Next(10) == 0)
			{
				this.SpawnItem((short)mod.ItemType<MotherboardTrophy>());
			}
			if (Main.rand.Next(3) == 0)
			{
				this.SpawnItem((short)mod.ItemType<BenderLegs>());
			}
			if (Main.rand.Next(10) == 0)
			{
				this.SpawnItem((short)mod.ItemType<FlaskCore>());
			}

			if (NPC.downedMoonlord && Main.rand.NextBool())
			{
				this.SpawnItem((short)mod.ItemType<CarbonSteel>(), Main.rand.Next(6, 12));
			}
		}

		// AI
		public void Teleport()
		{
			// npc.aiStyle = 2;
			npc.position += npc.velocity * 2;
		}

		public Rectangle GetFrame(int number)
		{
			return new Rectangle(0, npc.frame.Height * (number - 1), npc.frame.Width, npc.frame.Height);
		}

		public bool isInsideTerrain() {
			for (int i = (int)npc.position.X - 8; i < (npc.position.X + 8 + npc.width); i += 8)
				for (int l = (int)npc.Center.Y + 90; l < (npc.Center.Y + 106); l += 8)
					if (WorldGen.SolidTile(i / 16, l / 16))
						return true;
			return false;
		}

		// Changes phase (Following/disappearing/appearing)
		// this code should be revised
		private void CyclePhases()
		{
			--(stage.stateTime); // Lowering states time

			if (stage.stateTime <= 0) // If state time < or = 0 then update a variable
				stage.stateTime = stage.GetStateTime; // Updating

			// this was never actually executed
			// for (int i = 0; i < _clampers.Count; i++)
			//	Main.npc[_clampers[i]].ai[2] = 1;

			// If it is time to appear
			if (stage.stateTime <= GetAppearingTimeNow)
			{
				npc.ai[0] = -3; // Then appear
				return; // Ending the method
			}

			// If it is time to disappear
			if (stage.stateTime <= GetAppearingTimeNow + GetDisappearingTimeNow)
			{
				npc.ai[0] = -2; // Then disappear
				return; // Ending the method
			}

			// Otherwise it's time to follow..... maybe? stage2?
			if (npc.ai[0] == -2)
				stage.appearTime = GetAppearingTimeNow;

			if (--(stage.appearTime) > 0)
			{
				npc.ai[0] = -3;
				return;
			}

			// else
			npc.ai[0] = -1; // Follow the player
		}

		public override void AI()
		{
			stage.Animate(this);
			stage.AdjustHead(this);

			// fly away/enrage/whatever if needed (skeletron aiStyle)
			if (Helper.GetNearestPlayer(npc.position, true) == -1 || Main.dayTime)
			{
				npc.aiStyle = 11;
				npc.damage = 1000;
				npc.ai[0] = 2;
			}

			// if flying away
			if (npc.aiStyle == 11)
			{
				npc.rotation = 0;
				return;
			}

			// ini initialising
			if (stage == stage0)
			{
				stage = stage1;
				stage.Start(this);
			}

			// move between phases
			CyclePhases();

			// execute the stage's AI
			stage.AI(this);
		}

		// // ?? Doesn't seem to fix much
		// public override void SendExtraAI(BinaryWriter writer)
		// {
		//	writer.Write(_appearTime);
		//	writer.Write(AIStage);
		//	writer.Write(_signalDrones.Count);
		//	foreach (int drone in _signalDrones)
		//	{
		//		writer.Write(drone);
		//	}
		//	writer.Write(_lastSignalDron);
		//	writer.Write(_shootNow);
		//	writer.Write(_timeToNextDrone);
		//	writer.Write(_timeToShoot);
		//	writer.Write(_timeToLaser);
		//	writer.Write(_currentFrame);
		//	writer.Write(_timeToAnimation);
		//	writer.Write(_clampers.Count);
		//	foreach (int clamper in _clampers)
		//	{
		//		writer.Write(clamper);
		//	}
		//	writer.Write(_secondShootTime);
		// }

		// public override void ReceiveExtraAI(BinaryReader reader)
		// {
		//	_appearTime = reader.ReadInt32();
		//	AIStage = reader.ReadInt32();
		//	int c = reader.ReadInt32();
		//	_signalDrones = new List<int>();
		//	for (int i = 0; i < c; i++)
		//	{
		//		_signalDrones[i] = reader.ReadInt32();
		//	}
		//	_lastSignalDron = reader.ReadInt32();
		//	_shootNow = reader.ReadBoolean();
		//	_timeToNextDrone = reader.ReadInt32();
		//	_timeToShoot = reader.ReadInt32();
		//	_timeToLaser = reader.ReadInt32();
		//	_currentFrame = reader.ReadInt32();
		//	_timeToAnimation = reader.ReadInt32();
		//	c = reader.ReadInt32();
		//	for (int i = 0; i < c; i++)
		//	{
		//		_clampers[i] = reader.ReadInt32();
		//	}
		//	_secondShootTime = reader.ReadInt32();
		// }
	}
}
