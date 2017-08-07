using System;
using System.Collections.Generic;
using System.Linq;
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

		public virtual int FrameOffset => 0;

		public void Animate(Motherboard boss)
		{
			--_timeToAnimation;
			if (_timeToAnimation == 0)
			{
				_currentFrame = (_currentFrame + 1) % 3;
				_timeToAnimation = AnimationRate;
				boss.npc.frame = boss.GetFrame(_currentFrame + FrameOffset);
			}
		}

		public virtual void AI(Motherboard motherboard) {}
		public virtual void AdjustHead(Motherboard boss) {}
		public virtual void Start(Motherboard boss) {}
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
		public Stage1(int followPlayerTime, int disappearingTime, int appearingTime) : base(followPlayerTime, disappearingTime, appearingTime) {}

		private List<int> _signalDrones = new List<int>(); // ID of Signal Drones

		private const int DroneSpawnAreaX = 300; // Area size in which Drone can spawn by X value
		private const int DroneSpawnAreaY = 300; // Area size in which Drone can spawn by Y value
		private const int StartDroneCount = 8;
		private const int MaxDrones = 20;
		private const int ShootRate = 150; // Fire rate in ticks
		private const int TimeToLaserRate = 3; // Fire rate (From drones to player)

		private int _timeToNextDrone = 1; // Time for spawning next Drone
		private int _timeToShoot = 60; // Time for next shoot
		private int _timeToLaser = 3; // Time for next shoot (Drones lasers)
		private int _lastSignalDrone = -1; // Last Drone

		//------------------------------------------------
		// private methods
		//------------------------------------------------

		// Removes all dead Drones from the list
		private void RemoveDeadDrones(Motherboard boss)
		{
			List<int> aliveDronesList = _signalDrones.Where(x => Main.npc[x].active && Main.npc[x].type == boss.mod.NPCType("SignalDron")).ToList();
			_signalDrones = aliveDronesList;
		}

		// spawn and register one drone
		private void SpawnOneDrone(Motherboard boss)
		{
			Vector2 spawnPosition = Helper.RandomPointInArea(new Vector2(boss.npc.Center.X - DroneSpawnAreaX / 2, boss.npc.Center.Y - DroneSpawnAreaY / 2),
									 new Vector2(boss.npc.Center.X + DroneSpawnAreaX / 2, boss.npc.Center.Y + DroneSpawnAreaY / 2));
			_signalDrones.Add(NPC.NewNPC((int)spawnPosition.X, (int)spawnPosition.Y + LaserYOffset, boss.mod.NPCType("SignalDron"), 0, 0, 0, 0, boss.npc.whoAmI));
		}

		private void ShootOneLaser(Motherboard boss)
		{
			int whoAmIproj = (_lastSignalDrone == -1) ? boss.npc.whoAmI : _signalDrones[_lastSignalDrone];
			++_lastSignalDrone;
			int newProj = Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y, 0, 0,
							       boss.mod.ProjectileType("projMotherboardLaser"),
							       LaserDamage, LaserKb, 0, whoAmIproj, _signalDrones[_lastSignalDrone]);
			if (_lastSignalDrone == 0)
			{
				Main.projectile[newProj].localAI[1] = 1;
			}
		}

		private void ShootOneSecondShot(Motherboard boss)
		{
			Vector2 velocity = Helper.VelocityToPoint(Main.npc[_signalDrones[_signalDrones.Count - 1]].Center, Main.player[boss.npc.target].Center, SecondShootSpeed);
			velocity.X += Main.rand.Next(-SecondShootSpread, SecondShootSpread + 1) * SecondShootSpreadMult;
			velocity.Y += Main.rand.Next(-SecondShootSpread, SecondShootSpread + 1) * SecondShootSpreadMult;
			Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y,
						 velocity.X, velocity.Y,
						 ProjectileID.ShadowBeamHostile, SecondShootDamage, SecondShootKn);
		}

		private void ShootDroneLasers(Motherboard boss) // If it is time to shoot
		{
			// if there's no current drone AND we are not moving, return
			// only shoot lasers if there are any and we are in moving phase
			--_timeToLaser;
			if (_timeToLaser == 0)
			{
				// Set new shoot time (3 frames)
				_timeToLaser = TimeToLaserRate;
				ShootOneLaser(boss);

				// If we shot all interdrone lasers, shoot at the player
				if (_lastSignalDrone + 1 >= _signalDrones.Count)
				{
					// shoot N SecondShoots
					for (int i = 0; i < SecondShootCount; i++)
					{
						ShootOneSecondShot(boss);
					}

					// setting last Drone to -1 and ending the cycle of shooting
					_lastSignalDrone = -1;
					_timeToShoot = ShootRate;
				}
			}
		}

		//------------------------------------------------
		// hooks
		//------------------------------------------------

		public override void Start(Motherboard boss)
		{
			for (int i = 0; i < StartDroneCount; i++)
			{
				SpawnOneDrone(boss);
			}
		}

		public override void AdjustHead(Motherboard boss)
		{
			Main.npcHeadBossTexture[boss.headTexture] = boss.mod.GetTexture("NPCs/Motherboard_Head_Boss");
		}

		public override void AI(Motherboard boss)
		{
			RemoveDeadDrones(boss);

			if (_signalDrones.Count < MaxDrones)
			{
				--_timeToNextDrone;
				if (_timeToNextDrone < 0)
				{
					_timeToNextDrone = 60 * Main.rand.Next(3, 6);
					SpawnOneDrone(boss);
				}
			}

			if (_signalDrones.Count > 0)
			{
				--_timeToShoot;
				if (_timeToShoot < 0)
				{
					// only shoot lasers if there are any and we are in moving phase
					if (_lastSignalDrone > -1 && boss.npc.ai[0] == -1)
					{
						ShootDroneLasers(boss);
					}
				}
			}
			else
			{
				boss.stage = boss.stage2;
				boss.stage.Start(boss);
			}
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
		public Stage2(int followPlayerTime, int disappearingTime, int appearingTime) : base(followPlayerTime, disappearingTime, appearingTime) {}

		private List<int> _clampers = new List<int>(); // Clampers list

		public override int FrameOffset => 3;

		public override void AdjustHead(Motherboard boss)
		{
			Main.npcHeadBossTexture[boss.headTexture] = boss.mod.GetTexture("NPCs/Motherboard_Head_Boss");
		}

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
			List<int> aliveDronesList = _clampers.Where(x => Main.npc[x].active && Main.npc[x].type == boss.mod.NPCType("Clamper")).ToList();
			_clampers = aliveDronesList;

			// these clamper "lasers" are the strings?
			foreach (int ID in _clampers)
			{
				int id = Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y + LaserYOffset, 0, 0,
								  boss.mod.ProjectileType("projClamperLaser"), LaserDamage, LaserKb, 0, boss.npc.whoAmI, ID);
				Main.projectile[id].localAI[1] = stateTime;
			}
		}

		protected void SecondShoot(Motherboard boss)
		{
			if (!boss.isInsideTerrain()) {
				--_secondShootTime;
			}

			if (_secondShootTime <= 0)
			{
				_secondShootTime = SecondShootRate;
				for (int i = 0; i < 2; i++) {
					Projectile.NewProjectile(boss.npc.Center.X, boss.npc.Center.Y + 95, 0, 0,
								 boss.mod.ProjectileType("projMotherboardSuperLaser"), SecondShootDamage, SecondShootKn, 0, boss.npc.whoAmI, i);
				}
			}
		}

		public override void AI(Motherboard boss)
		{
			boss.Teleport();
			boss.npc.TargetClosest(true);

			// this was never actually executed
			// not sure what is meant to do, or where it is supposed to go
			// for (int i = 0; i < _clampers.Count; i++)
			//	Main.npc[_clampers[i]].ai[2] = 1;

			// following
			if (boss.npc.ai[1] == 0f)
			{
				// runs only SP/server side
				if (Main.netMode != 1)
				{
					// increment the something timer
					boss.npc.localAI[1] += 1f;

					// if the timer is due, plus some random amount of ticks
					if (boss.npc.localAI[1] >= 120 + Main.rand.Next(200))
					{
						boss.npc.localAI[1] = 0f;
						boss.npc.TargetClosest(true);

						// attempt to find coords somewhere around the target (max 100 tries)
						// break as soon as we find a place around the player that we can move to
						for (int attempts = 0; attempts < 100; attempts++)
						{
							int coordX = (int) Main.player[boss.npc.target].Center.X / 16 + Main.rand.Next(-50, 51);
							int coordY = (int) Main.player[boss.npc.target].Center.Y / 16 + Main.rand.Next(-50, 51);

							if (!WorldGen.SolidTile(coordX, coordY)
							    && Collision.CanHit(new Vector2(coordX * 16, coordY * 16), 1, 1,
										Main.player[boss.npc.target].position,
										Main.player[boss.npc.target].width,
										Main.player[boss.npc.target].height))
							{
								boss.npc.ai[1] = 1f;
								boss.npc.ai[2] = coordX;
								boss.npc.ai[3] = coordY;
								boss.npc.netUpdate = true;
								break;
							}
						}

						return;
					}
				}
			}
			// disappearing
			else if (boss.npc.ai[1] == 1f)
			{
				boss.npc.alpha = Math.Min(boss.npc.alpha + 3, 255);

				// finished disappearing
				if (boss.npc.alpha == 255)
				{
					boss.npc.position.X = boss.npc.ai[2] * 16f - boss.npc.width / 2;
					boss.npc.position.Y = boss.npc.ai[3] * 16f - boss.npc.height / 2;
					boss.npc.ai[1] = 2f;
					return;
				}
			}
			// appearing
			else if (boss.npc.ai[1] == 2f)
			{
				boss.npc.alpha = Math.Max(0, boss.npc.alpha - 3);

				// finished appearing
				if (boss.npc.alpha == 0)
				{
					boss.npc.ai[1] = 0f;
					return;
				}
			}

			// not finished appearing, disappearing, or didn't find a new place to move to....?
			CheckClampers(boss);
			SecondShoot(boss);
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
			return new Rectangle(0, npc.frame.Height * number, npc.frame.Width, npc.frame.Height);
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

			--(stage.appearTime);
			if (stage.appearTime > 0)
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
		//	writer.Write(_lastSignalDrone);
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
		//	_lastSignalDrone = reader.ReadInt32();
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
