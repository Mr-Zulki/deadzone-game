using System;
using System.Collections.Generic;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DEADZONE
{
    public partial class MainWindow : Window
    {
        // ═══════════════════════════════════════════════════════════════════
        //  CONSTANTS
        // ═══════════════════════════════════════════════════════════════════
        const double MAP_WIDTH = 1920;
        const double MAP_HEIGHT = 1080;
        const double PLAYER_SIZE = 48;
        const double PLAYER_SPEED = 4.0;
        const double BULLET_RADIUS = 5;
        const double HIT_RADIUS = 24;

        // ── Weapon stats ────────────────────────────────────────────────────
        const double PISTOL_DAMAGE = 20;
        const double PISTOL_SPEED = 11;
        const int PISTOL_COOLDOWN = 20;
        const int PISTOL_MAG = 12;
        const int PISTOL_RESERVE = 60;

        const double AKM_DAMAGE = 35;
        const double AKM_SPEED = 14;
        const int AKM_COOLDOWN = 8;
        const int AKM_MAG = 30;
        const int AKM_RESERVE = 90;

        const double SHOTGUN_DAMAGE = 15;
        const double SHOTGUN_SPEED = 10;
        const int SHOTGUN_COOLDOWN = 35;
        const int SHOTGUN_PELLETS = 6;
        const double SHOTGUN_SPREAD = 0.30;
        const int SHOTGUN_MAG = 8;
        const int SHOTGUN_RESERVE = 25;

        // ── Grenade stats ────────────────────────────────────────────────────
        const double GRENADE_DAMAGE = 80;
        const double GRENADE_RADIUS = 120;
        const double GRENADE_SPEED = 6.0;
        const int GRENADE_FUSE_FRAMES = 90;
        const double GRENADE_TRAVEL_DIST = 280;
        const int GRENADE_COOLDOWN = 180;
        const int P1_GRENADE_COUNT = 2;
        const int P2_GRENADE_COUNT = 2;

        // ── Blue Zone ───────────────────────────────────────────────────────
        double zoneCX, zoneCY, zoneRadius, zoneTargetRadius;
        const double ZONE_DAMAGE_PER_FRAME = 0.10;
        const int ZONE_WARN_FRAMES = 180;
        int zoneCountdown;
        bool zoneShrinking;
        readonly double[] zoneTargets = { 500, 300, 160, 80 };
        int zonePhaseIndex = 0;
        const int PHASE_FRAMES = 1800;
        int timerFrames = 0;

        // ═══════════════════════════════════════════════════════════════════
        //  SOUND SYSTEM
        // ═══════════════════════════════════════════════════════════════════
        // Music players — one for menu, one for gameplay
        MediaPlayer menuMusic = new MediaPlayer();
        MediaPlayer gameMusic = new MediaPlayer();

        // Sound effect players (MediaPlayer supports mp3/wav; SoundPlayer is WAV-only but instant)
        // We use MediaPlayer for each SFX slot so we can play mp3 too.
        // For rapid-fire sounds (bullets) we use a pool of SoundPlayers to avoid cutoff.
        MediaPlayer sfxDamageP1 = new MediaPlayer();
        MediaPlayer sfxDamageP2 = new MediaPlayer();
        MediaPlayer sfxShotgunShot = new MediaPlayer();
        MediaPlayer sfxAkmShot = new MediaPlayer();
        MediaPlayer sfxPistolShot = new MediaPlayer();
        MediaPlayer sfxGrenadeThrow = new MediaPlayer();
        MediaPlayer sfxGrenadeExplode = new MediaPlayer();
        MediaPlayer sfxHealthPickup = new MediaPlayer();
        MediaPlayer sfxWeaponPickup = new MediaPlayer();
        MediaPlayer sfxButtonClick = new MediaPlayer();
        MediaPlayer sfxWin = new MediaPlayer();
        MediaPlayer sfxZoneDamage = new MediaPlayer();
        MediaPlayer sfxEmptyGun = new MediaPlayer();

        // Track which music is currently playing so we can stop/switch
        enum MusicState { None, Menu, Game }
        MusicState currentMusic = MusicState.None;

        // Cooldowns so repeated sounds (zone damage, shoot) don't spam
        int sfxZoneCooldown = 0;
        int sfxPistolCooldown = 0;
        int sfxAkmCooldown = 0;
        int sfxShotgunCooldown = 0;

        // ═══════════════════════════════════════════════════════════════════
        //  WEAPON TYPE ENUM
        // ═══════════════════════════════════════════════════════════════════
        enum WeaponType { None, Pistol, AKM, Shotgun }

        // ═══════════════════════════════════════════════════════════════════
        //  PLAYER STATE
        // ═══════════════════════════════════════════════════════════════════
        double p1X = 180, p1Y = 490;
        double p2X = 1680, p2Y = 490;
        double p1HP = 100, p2HP = 100;
        double p1PrevHP = 100, p2PrevHP = 100;
        int p1Kills = 0, p2Kills = 0;
        bool p1Dead, p2Dead, isPaused;

        int p1Grenades = P1_GRENADE_COUNT;
        int p2Grenades = P2_GRENADE_COUNT;
        int p1GrenadeCooldown = 0;
        int p2GrenadeCooldown = 0;

        WeaponType[] p1Weapons = { WeaponType.Pistol, WeaponType.None };
        WeaponType[] p2Weapons = { WeaponType.Pistol, WeaponType.None };
        int p1ActiveSlot = 0;
        int p2ActiveSlot = 0;

        int[] p1Mag = { PISTOL_MAG, 0 };
        int[] p1Reserve = { PISTOL_RESERVE, 0 };
        int[] p2Mag = { PISTOL_MAG, 0 };
        int[] p2Reserve = { PISTOL_RESERVE, 0 };

        int p1ShootCooldown, p2ShootCooldown;

        int p1BlinkFrames = 0;
        int p2BlinkFrames = 0;
        const int BLINK_DURATION = 90;
        const int BLINK_PERIOD = 12;

        // ═══════════════════════════════════════════════════════════════════
        //  COLLECTIONS
        // ═══════════════════════════════════════════════════════════════════
        readonly HashSet<Key> keysDown = new HashSet<Key>();
        readonly List<Bullet> bullets = new List<Bullet>();
        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<HealthPickup> healthPacks = new List<HealthPickup>();
        readonly List<WeaponPickup> weaponDrops = new List<WeaponPickup>();
        readonly List<string> killFeedLog = new List<string>();
        readonly List<GrenadeProjectile> grenades = new List<GrenadeProjectile>();

        // ═══════════════════════════════════════════════════════════════════
        //  VISUAL ELEMENTS
        // ═══════════════════════════════════════════════════════════════════
        Ellipse p1Ring, p2Ring;
        UIElement p1Visual, p2Visual;
        Ellipse zoneCircle;
        DispatcherTimer gameLoop;
        // Legacy bgMusic kept for backward compat — we now route through menuMusic/gameMusic
        MediaPlayer bgMusic = new MediaPlayer();

        Border ZoneBanner => ZoneWarningBanner;
        TextBlock WinnerText => WinText;
        ProgressBar P1HpBar => P1HealthBar;
        ProgressBar P2HpBar => P2HealthBar;
        TextBlock P1HpText => P1HealthText;
        TextBlock P2HpText => P2HealthText;
        TextBlock P1KillText => P1Kills;
        TextBlock P2KillText => P2Kills;

        // ═══════════════════════════════════════════════════════════════════
        //  INNER CLASSES
        // ═══════════════════════════════════════════════════════════════════
        class Bullet
        {
            public double X, Y, VX, VY;
            public int Owner;
            public double Damage;
            public Ellipse Shape;
        }

        class Obstacle
        {
            public Rect Bounds;
            public bool Walkable;
            public bool IsCircle;
            public double CX, CY, CR;
        }

        class HealthPickup
        {
            public double X, Y;
            public UIElement Shape;
            public bool Taken;
        }

        class WeaponPickup
        {
            public double X, Y;
            public WeaponType Type;
            public UIElement Shape;
            public bool Taken;
        }

        class GrenadeProjectile
        {
            public double X, Y;
            public double VX, VY;
            public double TravelLeft;
            public bool Stopped;
            public int FuseFrames;
            public int Owner;
            public UIElement Body;
            public UIElement Shadow;
            public bool Exploded;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                Focus();
                Keyboard.Focus(this);
                InitSounds();
                PlayMenuMusic();
                InitGame();

                MenuPlay.MouseLeftButtonUp += (ms, me) =>
                {
                    PlaySfx(sfxButtonClick);
                    MainMenuOverlay.Visibility = Visibility.Collapsed;
                    Focus();
                    StartLoop();
                };

                MenuExit.MouseLeftButtonUp += (ms, me) =>
                {
                    PlaySfx(sfxButtonClick);
                    Application.Current.Shutdown();
                };

                MenuHowTo.MouseLeftButtonUp += (ms, me) =>
                {
                    PlaySfx(sfxButtonClick);
                    MainMenuOverlay.Visibility = Visibility.Collapsed;
                    HowToPlayOverlay.Visibility = Visibility.Visible;
                    ResetHtpPages();
                    // Menu music continues on How To Play screen
                };

                HtpBackBtn.MouseLeftButtonUp += (ms, me) =>
                {
                    PlaySfx(sfxButtonClick);
                    HowToPlayOverlay.Visibility = Visibility.Collapsed;
                    MainMenuOverlay.Visibility = Visibility.Visible;
                    PlayMenuMusic();
                };

                HtpNextBtn.Click += (ms, me) =>
                {
                    PlaySfx(sfxButtonClick);
                    if (HtpPage1.Visibility == Visibility.Visible)
                    {
                        HtpPage1.Visibility = Visibility.Collapsed;
                        HtpPage2.Visibility = Visibility.Visible;
                        HtpPageIndicator.Text = "PAGE 2 / 3";
                        HtpPrevBtn.IsEnabled = true;
                        HtpPrevBtn.Opacity = 1.0;
                    }
                    else if (HtpPage2.Visibility == Visibility.Visible)
                    {
                        HtpPage2.Visibility = Visibility.Collapsed;
                        HtpPage3.Visibility = Visibility.Visible;
                        HtpPageIndicator.Text = "PAGE 3 / 3";
                        HtpNextBtn.IsEnabled = false;
                        HtpNextBtn.Opacity = 0.4;
                    }
                };

                HtpPrevBtn.Click += (ms, me) =>
                {
                    PlaySfx(sfxButtonClick);
                    if (HtpPage2.Visibility == Visibility.Visible)
                    {
                        HtpPage2.Visibility = Visibility.Collapsed;
                        HtpPage1.Visibility = Visibility.Visible;
                        HtpPageIndicator.Text = "PAGE 1 / 3";
                        HtpPrevBtn.IsEnabled = false;
                        HtpPrevBtn.Opacity = 0.4;
                    }
                    else if (HtpPage3.Visibility == Visibility.Visible)
                    {
                        HtpPage3.Visibility = Visibility.Collapsed;
                        HtpPage2.Visibility = Visibility.Visible;
                        HtpPageIndicator.Text = "PAGE 2 / 3";
                        HtpNextBtn.IsEnabled = true;
                        HtpNextBtn.Opacity = 1.0;
                    }
                };
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SOUND SYSTEM INIT & HELPERS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load all audio assets. Each file is optional — missing files are silently skipped.
        /// 
        /// Expected files in Assets/Audio/ folder:
        ///   Music:
        ///     menu_music.mp3          — loops on main menu + how-to-play
        ///     game_music.mp3          — loops during gameplay
        ///   SFX:
        ///     sfx_pistol.mp3          — pistol shot
        ///     sfx_akm.mp3             — AKM/rifle shot
        ///     sfx_shotgun.mp3         — shotgun blast
        ///     sfx_damage.mp3          — player takes a hit (plays for both players)
        ///     sfx_grenade_throw.mp3   — grenade thrown
        ///     sfx_grenade_explode.mp3 — grenade detonation
        ///     sfx_health_pickup.mp3   — health pack collected
        ///     sfx_weapon_pickup.mp3   — weapon picked up
        ///     button_rizz.mp3    — UI button press
        ///     sfx_win.mp3             — victory fanfare
        ///     sfx_zone_damage.mp3     — zone tick damage alert
        ///     sfx_empty_gun.mp3       — dry-fire / no ammo click
        ///
        /// You can also place audio directly in Assets/ (the loader tries both paths).
        /// Supported formats: .mp3, .wav, .ogg (Windows Media Player codecs).
        /// </summary>
        void InitSounds()
        {
            // Menu music (loops)
            TryOpenMedia(menuMusic, "menu_music.wav");
            menuMusic.Volume = 0.45;
            menuMusic.MediaEnded += (s, e) => { menuMusic.Position = TimeSpan.Zero; menuMusic.Play(); };

            // Game music (loops)
            TryOpenMedia(gameMusic, "bgm.mp3");
            gameMusic.Volume = 0.4;
            gameMusic.MediaEnded += (s, e) => { gameMusic.Position = TimeSpan.Zero; gameMusic.Play(); };

            // SFX
            TryOpenMedia(sfxPistolShot, "placeholder.mp3");
            TryOpenMedia(sfxAkmShot, "placeholder.mp3");
            TryOpenMedia(sfxShotgunShot, "shotgun1.mp3");
            TryOpenMedia(sfxDamageP1, "zone_out.mp3");
            TryOpenMedia(sfxDamageP2, "zone_out.mp3");   // same file, separate player
            TryOpenMedia(sfxGrenadeThrow, "throw.mp3");
            TryOpenMedia(sfxGrenadeExplode, "gernade.mp3");
            TryOpenMedia(sfxHealthPickup, "healed-up.mp3");
            TryOpenMedia(sfxWeaponPickup, "hehe.mp3");
            TryOpenMedia(sfxButtonClick, "button_rizz.mp3");
            TryOpenMedia(sfxWin, "winner.mp3");
            TryOpenMedia(sfxZoneDamage, "zone_out.mp3");
            TryOpenMedia(sfxEmptyGun, "sfx_empty_gun.mp3");

            // Set SFX volumes
            foreach (var mp in new[] { sfxPistolShot, sfxAkmShot, sfxShotgunShot,
                                        sfxDamageP1, sfxDamageP2, sfxGrenadeThrow,
                                        sfxGrenadeExplode, sfxHealthPickup, sfxWeaponPickup,
                                        sfxButtonClick, sfxWin, sfxZoneDamage, sfxEmptyGun })
            {
                mp.Volume = 0.75;
            }
            sfxGrenadeExplode.Volume = 0.90;
            sfxWin.Volume = 0.85;
            sfxButtonClick.Volume = 0.60;
        }

        /// <summary>Tries Assets/Audio/ first, then Assets/ directly.</summary>
        void TryOpenMedia(MediaPlayer mp, string filename)
        {
            var candidates = new[]
            {
                $"pack://application:,,,/Assets/Audio/{filename}",
                $"pack://application:,,,/Assets/{filename}"
            };

            // MediaPlayer doesn't support pack:// URIs — resolve to absolute disk path
            string assemblyDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);

            string[] diskPaths =
            {
                System.IO.Path.Combine(assemblyDir, "Assets", "Audio", filename),
                System.IO.Path.Combine(assemblyDir, "Assets", filename)
            };

            foreach (var path in diskPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    try { mp.Open(new Uri(path, UriKind.Absolute)); return; }
                    catch { /* try next */ }
                }
            }
            // File not found — MediaPlayer simply won't play anything (no crash)
        }

        /// <summary>Restart a sound from the beginning and play it.</summary>
        void PlaySfx(MediaPlayer mp)
        {
            try
            {
                mp.Stop();
                mp.Position = TimeSpan.Zero;
                mp.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        void PlayMenuMusic()
        {
            if (currentMusic == MusicState.Menu) return;
            try { gameMusic.Stop(); } catch { }
            currentMusic = MusicState.Menu;
            try { menuMusic.Position = TimeSpan.Zero; menuMusic.Play(); } catch { }
        }

        void PlayGameMusic()
        {
            if (currentMusic == MusicState.Game) return;
            try { menuMusic.Stop(); } catch { }
            currentMusic = MusicState.Game;
            try { gameMusic.Position = TimeSpan.Zero; gameMusic.Play(); } catch { }
        }

        void StopAllMusic()
        {
            currentMusic = MusicState.None;
            try { menuMusic.Stop(); } catch { }
            try { gameMusic.Stop(); } catch { }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  INIT
        // ═══════════════════════════════════════════════════════════════════
        void InitGame()
        {
            GameCanvas.Children.Clear();
            bullets.Clear(); obstacles.Clear();
            healthPacks.Clear(); weaponDrops.Clear(); killFeedLog.Clear(); keysDown.Clear();
            grenades.Clear();

            p1X = 180; p1Y = 490; p1HP = 100; p1PrevHP = 100;
            p2X = 1680; p2Y = 490; p2HP = 100; p2PrevHP = 100;
            p1Kills = 0; p2Kills = 0;
            p1Dead = p2Dead = isPaused = false;
            p1ShootCooldown = p2ShootCooldown = 0;
            timerFrames = 0;

            p1Grenades = P1_GRENADE_COUNT;
            p2Grenades = P2_GRENADE_COUNT;
            p1GrenadeCooldown = p2GrenadeCooldown = 0;
            p1BlinkFrames = p2BlinkFrames = 0;

            sfxZoneCooldown = 0;
            sfxPistolCooldown = 0;
            sfxAkmCooldown = 0;
            sfxShotgunCooldown = 0;

            p1Weapons = new[] { WeaponType.Pistol, WeaponType.None };
            p2Weapons = new[] { WeaponType.Pistol, WeaponType.None };
            p1ActiveSlot = 0; p2ActiveSlot = 0;
            p1Mag = new[] { PISTOL_MAG, 0 };
            p1Reserve = new[] { PISTOL_RESERVE, 0 };
            p2Mag = new[] { PISTOL_MAG, 0 };
            p2Reserve = new[] { PISTOL_RESERVE, 0 };

            zoneCX = MAP_WIDTH / 2; zoneCY = MAP_HEIGHT / 2;
            zoneRadius = 900; zoneTargetRadius = zoneTargets[0];
            zonePhaseIndex = 0; zoneCountdown = PHASE_FRAMES; zoneShrinking = false;

            WinScreen.Visibility = Visibility.Collapsed;
            ZoneBanner.Visibility = Visibility.Collapsed;

            DrawBackground();
            DrawZoneCircle();
            DrawObstacles();
            SpawnPickups();
            DrawPlayers();
            UpdateHUD();
            UpdateKillFeed();
            LoadHudImages();
            ResetAvatarBlink();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  AVATAR BLINK SYSTEM
        // ═══════════════════════════════════════════════════════════════════
        void ResetAvatarBlink()
        {
            if (P1AvatarBlink != null) P1AvatarBlink.Opacity = 0;
            if (P2AvatarBlink != null) P2AvatarBlink.Opacity = 0;
        }

        void UpdateAvatarBlink()
        {
            bool p1Hit = p1HP < p1PrevHP;
            bool p2Hit = p2HP < p2PrevHP;

            // Trigger damage sounds when a player first takes damage this frame
            if (p1Hit && p1BlinkFrames <= 0) { p1BlinkFrames = BLINK_DURATION; PlaySfx(sfxDamageP1); }
            if (p2Hit && p2BlinkFrames <= 0) { p2BlinkFrames = BLINK_DURATION; PlaySfx(sfxDamageP2); }

            // P1 blink
            if (p1BlinkFrames > 0)
            {
                p1BlinkFrames--;
                bool on = (p1BlinkFrames / BLINK_PERIOD) % 2 == 0;
                double alpha = on ? 0.75 : 0.0;
                if (P1AvatarBlink != null) P1AvatarBlink.Opacity = alpha;
                if (P1HudBorder != null)
                    P1HudBorder.BorderBrush = on
                        ? new SolidColorBrush(Color.FromRgb(255, 40, 40))
                        : new SolidColorBrush(Color.FromRgb(91, 191, 52));
            }
            else
            {
                if (P1AvatarBlink != null) P1AvatarBlink.Opacity = 0;
                if (P1HudBorder != null)
                    P1HudBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(91, 191, 52));
            }

            // P2 blink
            if (p2BlinkFrames > 0)
            {
                p2BlinkFrames--;
                bool on = (p2BlinkFrames / BLINK_PERIOD) % 2 == 0;
                double alpha = on ? 0.75 : 0.0;
                if (P2AvatarBlink != null) P2AvatarBlink.Opacity = alpha;
                if (P2HudBorder != null)
                    P2HudBorder.BorderBrush = on
                        ? new SolidColorBrush(Color.FromRgb(255, 40, 40))
                        : new SolidColorBrush(Color.FromRgb(212, 59, 50));
            }
            else
            {
                if (P2AvatarBlink != null) P2AvatarBlink.Opacity = 0;
                if (P2HudBorder != null)
                    P2HudBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(212, 59, 50));
            }
        }

        // ─── Background ──────────────────────────────────────────────────────
        void DrawBackground()
        {
            AddZ(new Rectangle
            {
                Width = MAP_WIDTH,
                Height = MAP_HEIGHT,
                Fill = new SolidColorBrush(Color.FromRgb(18, 30, 18))
            }, 0, 0, 0);

            foreach (var name in new[] { "background map.png", "background map.jpg", "background map.jpeg" })
            {
                var src = TryLoadAsset(name);
                if (src != null)
                {
                    AddZ(new Image
                    {
                        Width = MAP_WIDTH,
                        Height = MAP_HEIGHT,
                        Source = src,
                        Stretch = Stretch.Fill,
                        Opacity = 1.0
                    }, 0, 0, 1);
                    break;
                }
            }

            for (int gx = 0; gx <= MAP_WIDTH; gx += 100)
                AddZ(new Line { X1 = gx, Y1 = 0, X2 = gx, Y2 = MAP_HEIGHT, Stroke = new SolidColorBrush(Color.FromArgb(15, 80, 130, 80)), StrokeThickness = 1 }, 0, 0, 2);
            for (int gy = 0; gy <= MAP_HEIGHT; gy += 100)
                AddZ(new Line { X1 = 0, Y1 = gy, X2 = MAP_WIDTH, Y2 = gy, Stroke = new SolidColorBrush(Color.FromArgb(15, 80, 130, 80)), StrokeThickness = 1 }, 0, 0, 2);
        }

        // ─── Zone Circle ─────────────────────────────────────────────────────
        void DrawZoneCircle()
        {
            zoneCircle = new Ellipse
            {
                Stroke = new SolidColorBrush(Color.FromArgb(220, 30, 144, 255)),
                StrokeThickness = 6,
                Fill = new SolidColorBrush(Color.FromArgb(18, 30, 144, 255))
            };
            AddZ(zoneCircle, 0, 0, 3);
            RefreshZoneVisual();
        }

        void RefreshZoneVisual()
        {
            double d = zoneRadius * 2;
            zoneCircle.Width = d;
            zoneCircle.Height = d;
            Canvas.SetLeft(zoneCircle, zoneCX - zoneRadius);
            Canvas.SetTop(zoneCircle, zoneCY - zoneRadius);
        }

        // ─── Obstacles ────────────────────────────────────────────────────────
        void DrawObstacles()
        {
            AddVisual("warehouse.png", 10, 370, 470, 350);
            AddCollision(30, 430, 210, 20);
            AddCollision(280, 470, 130, 20);
            AddCollision(80, 440, 20, 230);
            AddCollision(400, 460, 20, 150);
            AddCollision(80, 640, 100, 20);
            AddCollision(230, 600, 190, 20);

            AddVisual("warehouse2.png", 1480, 370, 470, 350);
            AddCollision(1730, 430, 150, 20);
            AddCollision(1530, 470, 140, 20);
            AddCollision(1850, 440, 20, 230);
            AddCollision(1530, 460, 20, 150);
            AddCollision(1780, 640, 100, 20);
            AddCollision(1530, 600, 200, 20);

            AddVisualWithCollision("wal1.png", 1300, 247, 80, 200, 1330, 250, 30, 180);
            AddVisualWithCollision("wal2.png", 370, 700, 100, 200, 400, 700, 30, 180);
            AddVisualWithCollision("wal2.png", 1430, 690, 80, 200, 1450, 700, 30, 180);
            AddVisualWithCollision("jeep.png", 810, 150, 230, 130, 835, 160, 190, 120);
            AddVisualWithCircleCollision("fountain2.png", 820, 380, 250, 300, 950, 540, 100);
            AddVisualWithCollision("grave.png", 910, 760, 100, 140, 930, 775, 60, 90);
            AddVisualWithCollision("grave.png", 970, 800, 100, 140, 990, 820, 70, 85);
        }

        void AddVisual(string file, double x, double y, double w, double h)
        {
            var src = TryLoadAsset(file);
            UIElement el = src != null
                ? (UIElement)new Image { Width = w, Height = h, Source = src, Stretch = Stretch.Fill }
                : new Rectangle { Width = w, Height = h, Fill = new SolidColorBrush(Color.FromRgb(80, 80, 80)), Opacity = 0.5 };
            AddZ(el, x, y, 4);
        }

        void AddCollision(double cx, double cy, double cw, double ch)
        {
            obstacles.Add(new Obstacle { Bounds = new Rect(cx, cy, cw, ch), Walkable = false });
        }

        void AddCircleCollision(double cx, double cy, double radius)
        {
            obstacles.Add(new Obstacle { IsCircle = true, CX = cx, CY = cy, CR = radius, Walkable = false });
        }

        void AddVisualWithCollision(string file, double vx, double vy, double vw, double vh, double cx, double cy, double cw, double ch)
        {
            AddVisual(file, vx, vy, vw, vh);
            AddCollision(cx, cy, cw, ch);
        }

        void AddVisualWithCircleCollision(string file, double vx, double vy, double vw, double vh, double cx, double cy, double cr)
        {
            AddVisual(file, vx, vy, vw, vh);
            AddCircleCollision(cx, cy, cr);
        }

        // ─── Pickups ──────────────────────────────────────────────────────────
        void SpawnPickups()
        {
            SpawnHealth(900, 130);
            SpawnHealth(1000, 600);
            SpawnWeapon(900, 350, WeaponType.AKM, "ak47.png");
            SpawnWeapon(900, 850, WeaponType.Shotgun, "Shoutgun.png");
        }

        void SpawnHealth(double x, double y)
        {
            var src = TryLoadAsset("Health.png");
            UIElement shape;
            if (src != null)
            {
                shape = new Image { Width = 38, Height = 38, Source = src };
            }
            else
            {
                var g = new Grid { Width = 38, Height = 38 };
                g.Children.Add(new Rectangle { Fill = Brushes.LimeGreen, Width = 38, Height = 10, VerticalAlignment = VerticalAlignment.Center });
                g.Children.Add(new Rectangle { Fill = Brushes.LimeGreen, Width = 10, Height = 38, HorizontalAlignment = HorizontalAlignment.Center });
                shape = g;
            }

            var ring = new Ellipse
            {
                Width = 42,
                Height = 42,
                Stroke = new SolidColorBrush(Colors.Yellow),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(60, 255, 220, 0)),
                Effect = new DropShadowEffect { Color = Colors.Yellow, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.7 }
            };
            AddZ(ring, x - 4, y - 6, 5);
            AddZ(shape, x, y, 5);
            healthPacks.Add(new HealthPickup { X = x, Y = y, Shape = shape });
        }

        void SpawnWeapon(double x, double y, WeaponType type, string file)
        {
            var src = TryLoadAsset(file);
            UIElement shape;
            if (src != null)
            {
                shape = new Image
                {
                    Width = 82,
                    Height = 62,
                    Source = src,
                    Stretch = Stretch.Fill,
                    Effect = new DropShadowEffect { Color = Colors.Yellow, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.7 }
                };
            }
            else
            {
                var color = type == WeaponType.AKM ? Colors.Gold : Colors.OrangeRed;
                shape = new Rectangle
                {
                    Width = 52,
                    Height = 20,
                    Fill = new SolidColorBrush(color),
                    RadiusX = 4,
                    RadiusY = 4,
                    Effect = new DropShadowEffect { Color = color, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 }
                };
            }
            var border = new Border
            {
                Width = 68,
                Height = 38,
                BorderBrush = new SolidColorBrush(Colors.Yellow),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 220, 0))
            };
            AddZ(border, x + 5, y + 8, 5);
            AddZ(shape, x, y, 6);
            weaponDrops.Add(new WeaponPickup { X = x, Y = y, Type = type, Shape = shape });
        }

        // ─── Players ─────────────────────────────────────────────────────────
        void DrawPlayers()
        {
            p1Ring = new Ellipse { Width = PLAYER_SIZE, Height = PLAYER_SIZE, Stroke = new SolidColorBrush(Color.FromArgb(200, 76, 175, 80)), StrokeThickness = 2.5, Fill = Brushes.Transparent };
            p2Ring = new Ellipse { Width = PLAYER_SIZE, Height = PLAYER_SIZE, Stroke = new SolidColorBrush(Color.FromArgb(200, 239, 83, 80)), StrokeThickness = 2.5, Fill = Brushes.Transparent };

            var src1 = TryLoadAsset("Player2.png");
            var src2 = TryLoadAsset("Player1.png");

            p1Visual = src1 != null
                ? (UIElement)new Image { Width = PLAYER_SIZE, Height = PLAYER_SIZE, Source = src1 }
                : new Ellipse { Width = PLAYER_SIZE, Height = PLAYER_SIZE, Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)) };

            p2Visual = src2 != null
                ? (UIElement)new Image { Width = PLAYER_SIZE, Height = PLAYER_SIZE, Source = src2 }
                : new Ellipse { Width = PLAYER_SIZE, Height = PLAYER_SIZE, Fill = new SolidColorBrush(Color.FromRgb(239, 83, 80)) };

            AddZ(p1Ring, 0, 0, 7); AddZ(p2Ring, 0, 0, 7);
            AddZ(p1Visual, 0, 0, 8); AddZ(p2Visual, 0, 0, 8);
            PlacePlayers();
        }

        void PlacePlayers()
        {
            Canvas.SetLeft(p1Ring, p1X); Canvas.SetTop(p1Ring, p1Y);
            Canvas.SetLeft(p2Ring, p2X); Canvas.SetTop(p2Ring, p2Y);
            Canvas.SetLeft(p1Visual, p1X); Canvas.SetTop(p1Visual, p1Y);
            Canvas.SetLeft(p2Visual, p2X); Canvas.SetTop(p2Visual, p2Y);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  GAME LOOP
        // ═══════════════════════════════════════════════════════════════════
        void StartLoop()
        {
            PlayGameMusic();
            gameLoop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            gameLoop.Tick += (s, e) => Tick();
            gameLoop.Start();
        }

        void Tick()
        {
            if (isPaused || p1Dead || p2Dead) return;
            timerFrames++;

            // Decrement sound cooldowns
            if (sfxZoneCooldown > 0) sfxZoneCooldown--;
            if (sfxPistolCooldown > 0) sfxPistolCooldown--;
            if (sfxAkmCooldown > 0) sfxAkmCooldown--;
            if (sfxShotgunCooldown > 0) sfxShotgunCooldown--;

            p1PrevHP = p1HP;
            p2PrevHP = p2HP;

            HandleInput();
            UpdateZone();
            MoveBullets();
            CheckBulletWalls();
            CheckBulletHits();
            CheckHealthPickups();
            CheckWeaponPickups();
            CheckZoneDamage();
            UpdateGrenades();

            UpdateAvatarBlink();
            PlacePlayers();
            UpdateHUD();
            CheckWin();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  INPUT
        // ═══════════════════════════════════════════════════════════════════
        void HandleInput()
        {
            MovePlayer(ref p1X, ref p1Y, Key.W, Key.S, Key.A, Key.D);
            MovePlayer(ref p2X, ref p2Y, Key.Up, Key.Down, Key.Left, Key.Right);

            double p1CX = p1X + PLAYER_SIZE / 2, p1CY = p1Y + PLAYER_SIZE / 2;
            double p2CX = p2X + PLAYER_SIZE / 2, p2CY = p2Y + PLAYER_SIZE / 2;

            // ── Shoot ──────────────────────────────────────────────────────────
            if (keysDown.Contains(Key.D1) && p1ShootCooldown == 0)
            {
                FireWeapon(1, p1ActiveSlot, p1CX, p1CY, p2CX - p1CX, p2CY - p1CY);
                p1ShootCooldown = GetCooldown(p1Weapons[p1ActiveSlot]);
            }
            if (keysDown.Contains(Key.I) && p2ShootCooldown == 0)
            {
                FireWeapon(2, p2ActiveSlot, p2CX, p2CY, p1CX - p2CX, p1CY - p2CY);
                p2ShootCooldown = GetCooldown(p2Weapons[p2ActiveSlot]);
            }

            if (p1ShootCooldown > 0) p1ShootCooldown--;
            if (p2ShootCooldown > 0) p2ShootCooldown--;

            // ── Grenade: P1 = key 3 ────────────────────────────────────────────
            if (keysDown.Contains(Key.D3) && p1Grenades > 0 && p1GrenadeCooldown == 0)
            {
                ThrowGrenade(1, p1CX, p1CY, p2CX - p1CX, p2CY - p1CY);
                p1Grenades--;
                p1GrenadeCooldown = GRENADE_COOLDOWN;
            }
            // ── Grenade: P2 = P ─────────────────────────────────────────
            if (keysDown.Contains(Key.P) && p2Grenades > 0 && p2GrenadeCooldown == 0)
            {
                ThrowGrenade(2, p2CX, p2CY, p1CX - p2CX, p1CY - p2CY);
                p2Grenades--;
                p2GrenadeCooldown = GRENADE_COOLDOWN;
            }

            if (p1GrenadeCooldown > 0) p1GrenadeCooldown--;
            if (p2GrenadeCooldown > 0) p2GrenadeCooldown--;
        }

        int GetCooldown(WeaponType w)
        {
            switch (w)
            {
                case WeaponType.AKM: return AKM_COOLDOWN;
                case WeaponType.Shotgun: return SHOTGUN_COOLDOWN;
                default: return PISTOL_COOLDOWN;
            }
        }

        void MovePlayer(ref double x, ref double y, Key up, Key dn, Key lt, Key rt)
        {
            double nx = x, ny = y;
            if (keysDown.Contains(up)) ny -= PLAYER_SPEED;
            if (keysDown.Contains(dn)) ny += PLAYER_SPEED;
            if (keysDown.Contains(lt)) nx -= PLAYER_SPEED;
            if (keysDown.Contains(rt)) nx += PLAYER_SPEED;

            nx = Math.Max(0, Math.Min(MAP_WIDTH - PLAYER_SIZE, nx));
            ny = Math.Max(0, Math.Min(MAP_HEIGHT - PLAYER_SIZE, ny));

            if (!HitsObstacle(nx, y)) x = nx;
            if (!HitsObstacle(x, ny)) y = ny;
        }

        bool HitsObstacle(double x, double y)
        {
            double shrink = 6;
            var r = new Rect(x + shrink, y + shrink, PLAYER_SIZE - shrink * 2, PLAYER_SIZE - shrink * 2);
            double pCX = x + PLAYER_SIZE / 2, pCY = y + PLAYER_SIZE / 2;

            foreach (var o in obstacles)
            {
                if (o.Walkable) continue;
                if (o.IsCircle) { if (Dist(pCX, pCY, o.CX, o.CY) < o.CR + PLAYER_SIZE / 2) return true; }
                else { if (r.IntersectsWith(o.Bounds)) return true; }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SHOOTING
        // ═══════════════════════════════════════════════════════════════════
        void FireWeapon(int owner, int slot, double cx, double cy, double dx, double dy)
        {
            var weapons = owner == 1 ? p1Weapons : p2Weapons;
            var mag = owner == 1 ? p1Mag : p2Mag;
            var reserve = owner == 1 ? p1Reserve : p2Reserve;

            WeaponType wt = weapons[slot];
            if (wt == WeaponType.None) return;

            if (mag[slot] <= 0)
            {
                // Dry-fire / auto-reload
                int need = MagSize(wt) - mag[slot];
                int reload = Math.Min(need, reserve[slot]);
                mag[slot] += reload;
                reserve[slot] -= reload;
                if (mag[slot] <= 0)
                {
                    PlaySfx(sfxEmptyGun);   // completely out of ammo
                    return;
                }
            }

            mag[slot]--;

            // ── Play the correct shot SFX with per-weapon cooldown ──────────
            switch (wt)
            {
                case WeaponType.Pistol:
                    if (sfxPistolCooldown == 0) { PlaySfx(sfxPistolShot); sfxPistolCooldown = PISTOL_COOLDOWN; }
                    break;
                case WeaponType.AKM:
                    if (sfxAkmCooldown == 0) { PlaySfx(sfxAkmShot); sfxAkmCooldown = AKM_COOLDOWN; }
                    break;
                case WeaponType.Shotgun:
                    if (sfxShotgunCooldown == 0) { PlaySfx(sfxShotgunShot); sfxShotgunCooldown = SHOTGUN_COOLDOWN; }
                    break;
            }

            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) { dx = 1; dy = 0; } else { dx /= len; dy /= len; }

            if (wt == WeaponType.Shotgun)
            {
                for (int i = 0; i < SHOTGUN_PELLETS; i++)
                {
                    double angle = Math.Atan2(dy, dx) + (i - SHOTGUN_PELLETS / 2.0) * (SHOTGUN_SPREAD / SHOTGUN_PELLETS);
                    SpawnBullet(owner, cx, cy, Math.Cos(angle), Math.Sin(angle), SHOTGUN_SPEED, SHOTGUN_DAMAGE);
                }
            }
            else
            {
                double spd = wt == WeaponType.AKM ? AKM_SPEED : PISTOL_SPEED;
                double dmg = wt == WeaponType.AKM ? AKM_DAMAGE : PISTOL_DAMAGE;
                SpawnBullet(owner, cx, cy, dx, dy, spd, dmg);
            }
        }

        int MagSize(WeaponType w)
        {
            switch (w)
            {
                case WeaponType.AKM: return AKM_MAG;
                case WeaponType.Shotgun: return SHOTGUN_MAG;
                default: return PISTOL_MAG;
            }
        }

        void SpawnBullet(int owner, double cx, double cy, double dx, double dy, double speed, double damage)
        {
            var color = owner == 1 ? Color.FromRgb(255, 225, 50) : Color.FromRgb(255, 100, 30);
            var b = new Bullet
            {
                X = cx - BULLET_RADIUS,
                Y = cy - BULLET_RADIUS,
                VX = dx * speed,
                VY = dy * speed,
                Owner = owner,
                Damage = damage,
                Shape = new Ellipse
                {
                    Width = BULLET_RADIUS * 2,
                    Height = BULLET_RADIUS * 2,
                    Fill = new SolidColorBrush(color),
                    Effect = new DropShadowEffect { Color = owner == 1 ? Colors.Yellow : Colors.OrangeRed, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 }
                }
            };
            AddZ(b.Shape, b.X, b.Y, 9);
            bullets.Add(b);
        }

        void MoveBullets()
        {
            var dead = new List<Bullet>();
            foreach (var b in bullets)
            {
                b.X += b.VX; b.Y += b.VY;
                Canvas.SetLeft(b.Shape, b.X);
                Canvas.SetTop(b.Shape, b.Y);
                if (b.X < -30 || b.X > MAP_WIDTH + 30 || b.Y < -30 || b.Y > MAP_HEIGHT + 30)
                    dead.Add(b);
            }
            KillBullets(dead);
        }

        void CheckBulletWalls()
        {
            var dead = new List<Bullet>();
            foreach (var b in bullets)
            {
                double bCX = b.X + BULLET_RADIUS, bCY = b.Y + BULLET_RADIUS;
                foreach (var o in obstacles)
                {
                    if (o.Walkable) continue;
                    if (o.IsCircle) { if (Dist(bCX, bCY, o.CX, o.CY) <= o.CR + BULLET_RADIUS) { dead.Add(b); break; } }
                    else { var br = new Rect(b.X, b.Y, BULLET_RADIUS * 2, BULLET_RADIUS * 2); if (br.IntersectsWith(o.Bounds)) { dead.Add(b); break; } }
                }
            }
            KillBullets(dead);
        }

        void CheckBulletHits()
        {
            var dead = new List<Bullet>();
            double p1CX = p1X + PLAYER_SIZE / 2, p1CY = p1Y + PLAYER_SIZE / 2;
            double p2CX = p2X + PLAYER_SIZE / 2, p2CY = p2Y + PLAYER_SIZE / 2;

            foreach (var b in bullets)
            {
                double bCX = b.X + BULLET_RADIUS, bCY = b.Y + BULLET_RADIUS;
                if (b.Owner == 1 && Dist(bCX, bCY, p2CX, p2CY) < HIT_RADIUS)
                {
                    p2HP = Math.Max(0, p2HP - b.Damage);
                    dead.Add(b);
                    AddKill($"P1 hit P2  −{(int)b.Damage} HP");
                }
                else if (b.Owner == 2 && Dist(bCX, bCY, p1CX, p1CY) < HIT_RADIUS)
                {
                    p1HP = Math.Max(0, p1HP - b.Damage);
                    dead.Add(b);
                    AddKill($"P2 hit P1  −{(int)b.Damage} HP");
                }
            }
            KillBullets(dead);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  GRENADE SYSTEM
        // ═══════════════════════════════════════════════════════════════════
        void ThrowGrenade(int owner, double fromX, double fromY, double dx, double dy)
        {
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) { dx = 1; dy = 0; } else { dx /= len; dy /= len; }

            // Play throw SFX
            PlaySfx(sfxGrenadeThrow);

            var shadow = new Ellipse
            {
                Width = 22,
                Height = 10,
                Fill = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
            };

            var grenadeBody = new Grid { Width = 22, Height = 22 };
            var outer = new Ellipse
            {
                Width = 22,
                Height = 22,
                Fill = new RadialGradientBrush(
                    Color.FromRgb(110, 130, 60),
                    Color.FromRgb(50, 65, 25))
                {
                    GradientOrigin = new Point(0.35, 0.3),
                    Center = new Point(0.5, 0.5),
                    RadiusX = 0.6,
                    RadiusY = 0.6
                },
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.7 }
            };
            var lever = new Rectangle
            {
                Width = 8,
                Height = 3,
                Fill = new SolidColorBrush(Color.FromRgb(200, 180, 80)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 0, 0)
            };
            grenadeBody.Children.Add(outer);
            grenadeBody.Children.Add(lever);

            AddZ(shadow, fromX - 11, fromY + 10, 5);
            AddZ(grenadeBody, fromX - 11, fromY - 11, 10);

            grenades.Add(new GrenadeProjectile
            {
                X = fromX,
                Y = fromY,
                VX = dx * GRENADE_SPEED,
                VY = dy * GRENADE_SPEED,
                TravelLeft = GRENADE_TRAVEL_DIST,
                Stopped = false,
                FuseFrames = GRENADE_FUSE_FRAMES,
                Owner = owner,
                Body = grenadeBody,
                Shadow = shadow,
                Exploded = false
            });

            AddKill($"💣 P{owner} threw a grenade!");
        }

        void UpdateGrenades()
        {
            var done = new List<GrenadeProjectile>();

            foreach (var g in grenades)
            {
                if (g.Exploded) { done.Add(g); continue; }

                if (!g.Stopped)
                {
                    double step = Math.Sqrt(g.VX * g.VX + g.VY * g.VY);
                    g.X += g.VX; g.Y += g.VY;
                    g.TravelLeft -= step;

                    g.X = Math.Max(10, Math.Min(MAP_WIDTH - 10, g.X));
                    g.Y = Math.Max(10, Math.Min(MAP_HEIGHT - 10, g.Y));

                    Canvas.SetLeft(g.Body, g.X - 11);
                    Canvas.SetTop(g.Body, g.Y - 11);
                    Canvas.SetLeft(g.Shadow, g.X - 11);
                    Canvas.SetTop(g.Shadow, g.Y + 8);

                    double progress = 1.0 - (g.TravelLeft / GRENADE_TRAVEL_DIST);
                    double arcScale = 1.0 - 2.2 * Math.Abs(progress - 0.5);
                    arcScale = Math.Max(0.3, arcScale);
                    g.Shadow.RenderTransform = new ScaleTransform(arcScale, arcScale, 11, 5);

                    double bobOffset = -18 * Math.Sin(progress * Math.PI);
                    Canvas.SetTop(g.Body, g.Y - 11 + bobOffset);

                    if (g.TravelLeft <= 0)
                    {
                        g.Stopped = true;
                        Canvas.SetTop(g.Body, g.Y - 11);
                        g.Shadow.RenderTransform = null;
                    }
                }
                else
                {
                    g.FuseFrames--;

                    bool blink = (g.FuseFrames / 6) % 2 == 0;
                    if (g.Body is Grid gb && gb.Children.Count > 0 && gb.Children[0] is Ellipse ell)
                    {
                        ell.Fill = blink
                            ? new RadialGradientBrush(Color.FromRgb(220, 80, 30), Color.FromRgb(160, 30, 10)) { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.5, 0.5), RadiusX = 0.6, RadiusY = 0.6 }
                            : new RadialGradientBrush(Color.FromRgb(110, 130, 60), Color.FromRgb(50, 65, 25)) { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.5, 0.5), RadiusX = 0.6, RadiusY = 0.6 };
                    }

                    if (g.FuseFrames <= 0)
                    {
                        ExplodeGrenade(g);
                        done.Add(g);
                    }
                }
            }

            foreach (var g in done)
            {
                if (g.Body != null) GameCanvas.Children.Remove(g.Body);
                if (g.Shadow != null) GameCanvas.Children.Remove(g.Shadow);
                grenades.Remove(g);
            }
        }

        void ExplodeGrenade(GrenadeProjectile g)
        {
            // Play explosion SFX
            PlaySfx(sfxGrenadeExplode);

            double p1CX = p1X + PLAYER_SIZE / 2, p1CY = p1Y + PLAYER_SIZE / 2;
            double p2CX = p2X + PLAYER_SIZE / 2, p2CY = p2Y + PLAYER_SIZE / 2;

            if (g.Owner == 1 && Dist(g.X, g.Y, p2CX, p2CY) <= GRENADE_RADIUS)
            {
                double falloff = 1.0 - Dist(g.X, g.Y, p2CX, p2CY) / GRENADE_RADIUS;
                double dmg = GRENADE_DAMAGE * Math.Max(0.3, falloff);
                p2HP = Math.Max(0, p2HP - dmg);
                AddKill($"💥 P1 grenade hit P2  −{(int)dmg} HP");
            }
            if (g.Owner == 2 && Dist(g.X, g.Y, p1CX, p1CY) <= GRENADE_RADIUS)
            {
                double falloff = 1.0 - Dist(g.X, g.Y, p1CX, p1CY) / GRENADE_RADIUS;
                double dmg = GRENADE_DAMAGE * Math.Max(0.3, falloff);
                p1HP = Math.Max(0, p1HP - dmg);
                AddKill($"💥 P2 grenade hit P1  −{(int)dmg} HP");
            }

            SpawnExplosion(g.X, g.Y);
            g.Exploded = true;
        }

        void SpawnExplosion(double cx, double cy)
        {
            var shockwave = new Ellipse
            {
                Width = 20,
                Height = 20,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 180, 50)),
                StrokeThickness = 5,
                Fill = Brushes.Transparent
            };
            AddZ(shockwave, cx - 10, cy - 10, 15);

            var fireball = new Ellipse
            {
                Width = 30,
                Height = 30,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(255, 255, 240, 100),
                    Color.FromArgb(180, 255, 80, 0))
                { GradientOrigin = new Point(0.4, 0.4), Center = new Point(0.5, 0.5), RadiusX = 0.6, RadiusY = 0.6 },
                Effect = new DropShadowEffect { Color = Colors.OrangeRed, BlurRadius = 30, ShadowDepth = 0, Opacity = 1.0 }
            };
            AddZ(fireball, cx - 15, cy - 15, 16);

            var smoke = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new RadialGradientBrush(Color.FromArgb(160, 100, 90, 80), Color.FromArgb(0, 60, 55, 50)),
                Opacity = 0.85
            };
            AddZ(smoke, cx - 5, cy - 5, 14);

            var damageRing = new Ellipse
            {
                Width = GRENADE_RADIUS * 2,
                Height = GRENADE_RADIUS * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 100, 0)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(25, 255, 80, 0)),
                Effect = new DropShadowEffect { Color = Colors.OrangeRed, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.5 }
            };
            AddZ(damageRing, cx - GRENADE_RADIUS, cy - GRENADE_RADIUS, 13);

            int frame = 0;
            const int TOTAL_FRAMES = 35;
            const int SMOKE_FRAMES = 55;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (s, e) =>
            {
                frame++;
                double t = frame / (double)TOTAL_FRAMES;

                double swSize = 20 + GRENADE_RADIUS * 2.2 * t;
                shockwave.Width = swSize; shockwave.Height = swSize;
                Canvas.SetLeft(shockwave, cx - swSize / 2); Canvas.SetTop(shockwave, cy - swSize / 2);
                shockwave.Opacity = Math.Max(0, 1.0 - t * 1.2);
                shockwave.StrokeThickness = Math.Max(1, 5 * (1 - t));

                double tc = Math.Min(t, 1.0);
                double fbScale = tc < 0.4 ? (tc / 0.4) : (1.0 - (tc - 0.4) / 0.6);
                fbScale = Math.Max(0, fbScale);
                double fbSize = Math.Max(0, 30 + 90 * fbScale);
                fireball.Width = fbSize; fireball.Height = fbSize;
                Canvas.SetLeft(fireball, cx - fbSize / 2); Canvas.SetTop(fireball, cy - fbSize / 2);
                fireball.Opacity = Math.Max(0, 1.0 - tc);

                double drSize = GRENADE_RADIUS * 2 * (0.5 + 0.5 * t);
                damageRing.Width = drSize; damageRing.Height = drSize;
                Canvas.SetLeft(damageRing, cx - drSize / 2); Canvas.SetTop(damageRing, cy - drSize / 2);
                damageRing.Opacity = Math.Max(0, 0.7 - t * 1.1);

                double smSize = 10 + 140 * t;
                smoke.Width = smSize; smoke.Height = smSize;
                Canvas.SetLeft(smoke, cx - smSize / 2); Canvas.SetTop(smoke, cy - smSize / 2);
                smoke.Opacity = frame < SMOKE_FRAMES ? Math.Max(0, 0.85 - (frame / (double)SMOKE_FRAMES) * 0.85) : 0;

                if (frame >= SMOKE_FRAMES)
                {
                    GameCanvas.Children.Remove(shockwave);
                    GameCanvas.Children.Remove(fireball);
                    GameCanvas.Children.Remove(smoke);
                    GameCanvas.Children.Remove(damageRing);
                    timer.Stop();
                }
            };
            timer.Start();

            var rand = new Random();
            for (int i = 0; i < 14; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double speed = 3 + rand.NextDouble() * 5;
                double pvx = Math.Cos(angle) * speed;
                double pvy = Math.Sin(angle) * speed;
                double px = cx, py = cy;
                bool isEmber = rand.NextDouble() > 0.5;

                var particle = new Ellipse
                {
                    Width = isEmber ? 5 : 3,
                    Height = isEmber ? 5 : 3,
                    Fill = new SolidColorBrush(isEmber
                        ? Color.FromRgb(255, (byte)rand.Next(100, 200), 20)
                        : Color.FromRgb(80, 70, 60))
                };
                AddZ(particle, px, py, 17);

                int pf = 0;
                var ptimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                ptimer.Tick += (s, e) =>
                {
                    pf++;
                    px += pvx; py += pvy;
                    pvy += 0.3;
                    pvx *= 0.93;
                    Canvas.SetLeft(particle, px); Canvas.SetTop(particle, py);
                    particle.Opacity = Math.Max(0, 1.0 - pf / 25.0);
                    if (pf >= 25) { GameCanvas.Children.Remove(particle); ptimer.Stop(); }
                };
                ptimer.Start();
            }

            var flash = new Rectangle
            {
                Width = MAP_WIDTH,
                Height = MAP_HEIGHT,
                Fill = new SolidColorBrush(Color.FromArgb(60, 255, 200, 100)),
                IsHitTestVisible = false
            };
            AddZ(flash, 0, 0, 18);
            int ff = 0;
            var ftimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            ftimer.Tick += (s, e) =>
            {
                ff++;
                flash.Opacity = Math.Max(0, 0.6 - ff * 0.15);
                if (ff >= 5) { GameCanvas.Children.Remove(flash); ftimer.Stop(); }
            };
            ftimer.Start();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PICKUPS
        // ═══════════════════════════════════════════════════════════════════
        void CheckHealthPickups()
        {
            var gone = new List<HealthPickup>();
            double p1CX = p1X + PLAYER_SIZE / 2, p1CY = p1Y + PLAYER_SIZE / 2;
            double p2CX = p2X + PLAYER_SIZE / 2, p2CY = p2Y + PLAYER_SIZE / 2;

            foreach (var hp in healthPacks)
            {
                if (hp.Taken) continue;
                double hCX = hp.X + 19, hCY = hp.Y + 19;
                if (Dist(p1CX, p1CY, hCX, hCY) < 38)
                {
                    p1HP = 100; hp.Taken = true; gone.Add(hp);
                    PlaySfx(sfxHealthPickup);
                    AddKill("✚ P1 picked up health!");
                }
                else if (Dist(p2CX, p2CY, hCX, hCY) < 38)
                {
                    p2HP = 100; hp.Taken = true; gone.Add(hp);
                    PlaySfx(sfxHealthPickup);
                    AddKill("✚ P2 picked up health!");
                }
            }
            foreach (var hp in gone) { GameCanvas.Children.Remove(hp.Shape); healthPacks.Remove(hp); }
        }

        void CheckWeaponPickups()
        {
            var gone = new List<WeaponPickup>();
            double p1CX = p1X + PLAYER_SIZE / 2, p1CY = p1Y + PLAYER_SIZE / 2;
            double p2CX = p2X + PLAYER_SIZE / 2, p2CY = p2Y + PLAYER_SIZE / 2;

            foreach (var wp in weaponDrops)
            {
                if (wp.Taken) continue;
                double wCX = wp.X + 26, wCY = wp.Y + 16;
                if (Dist(p1CX, p1CY, wCX, wCY) < 45)
                {
                    GiveWeapon(1, wp.Type); wp.Taken = true; gone.Add(wp);
                    PlaySfx(sfxWeaponPickup);
                    AddKill($"P1 picked up {wp.Type}!");
                }
                else if (Dist(p2CX, p2CY, wCX, wCY) < 45)
                {
                    GiveWeapon(2, wp.Type); wp.Taken = true; gone.Add(wp);
                    PlaySfx(sfxWeaponPickup);
                    AddKill($"P2 picked up {wp.Type}!");
                }
            }
            foreach (var wp in gone) { GameCanvas.Children.Remove(wp.Shape); weaponDrops.Remove(wp); }
        }

        void GiveWeapon(int player, WeaponType type)
        {
            var weapons = player == 1 ? p1Weapons : p2Weapons;
            var mag = player == 1 ? p1Mag : p2Mag;
            var reserve = player == 1 ? p1Reserve : p2Reserve;

            weapons[1] = type;
            mag[1] = MagSize(type);
            reserve[1] = type == WeaponType.AKM ? AKM_RESERVE : SHOTGUN_RESERVE;

            ImageSource gunImage = null;
            switch (type)
            {
                case WeaponType.AKM: gunImage = TryLoadAsset("ak47.png"); break;
                case WeaponType.Shotgun: gunImage = TryLoadAsset("Shoutgun.png"); break;
            }

            if (player == 1) P1Weapon2Image.Source = gunImage;
            else P2Weapon2Image.Source = gunImage;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ZONE
        // ═══════════════════════════════════════════════════════════════════
        void CheckZoneDamage()
        {
            double p1CX = p1X + PLAYER_SIZE / 2, p1CY = p1Y + PLAYER_SIZE / 2;
            double p2CX = p2X + PLAYER_SIZE / 2, p2CY = p2Y + PLAYER_SIZE / 2;

            bool anyZoneDamage = false;

            if (Dist(p1CX, p1CY, zoneCX, zoneCY) > zoneRadius)
            {
                p1HP = Math.Max(0, p1HP - ZONE_DAMAGE_PER_FRAME);
                anyZoneDamage = true;
                if (timerFrames % 60 == 0) AddKill("⚠ P1 taking zone damage!");
            }
            if (Dist(p2CX, p2CY, zoneCX, zoneCY) > zoneRadius)
            {
                p2HP = Math.Max(0, p2HP - ZONE_DAMAGE_PER_FRAME);
                anyZoneDamage = true;
                if (timerFrames % 60 == 0) AddKill("⚠ P2 taking zone damage!");
            }

            // Play zone alarm once every ~2 seconds while someone is outside
            if (anyZoneDamage && sfxZoneCooldown == 0)
            {
                PlaySfx(sfxZoneDamage);
                sfxZoneCooldown = 120; // 2 seconds at 60fps
            }
        }

        void UpdateZone()
        {
            zoneCountdown--;
            bool showBanner = zoneCountdown <= ZONE_WARN_FRAMES && !zoneShrinking;
            ZoneBanner.Visibility = showBanner ? Visibility.Visible : Visibility.Collapsed;

            if (zoneCountdown <= 0 && !zoneShrinking) { zoneShrinking = true; ZoneBanner.Visibility = Visibility.Visible; }

            if (zoneShrinking)
            {
                double diff = zoneRadius - zoneTargetRadius;
                if (Math.Abs(diff) < 1.5)
                {
                    zoneRadius = zoneTargetRadius; zoneShrinking = false;
                    ZoneBanner.Visibility = Visibility.Collapsed;
                    zonePhaseIndex++;
                    if (zonePhaseIndex < zoneTargets.Length) { zoneTargetRadius = zoneTargets[zonePhaseIndex]; zoneCountdown = PHASE_FRAMES; }
                }
                else { zoneRadius -= Math.Min(0.5, diff * 0.015); }
            }
            RefreshZoneVisual();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  WIN CHECK
        // ═══════════════════════════════════════════════════════════════════
        void CheckWin()
        {
            if (p1HP <= 0 && !p1Dead) { p1Dead = true; p2Kills++; AddKill("💀 P2 eliminated P1!"); ShowWin("PLAYER 2 WINS!"); }
            if (p2HP <= 0 && !p2Dead) { p2Dead = true; p1Kills++; AddKill("💀 P1 eliminated P2!"); ShowWin("PLAYER 1 WINS!"); }
        }

        void ShowWin(string msg)
        {
            gameLoop.Stop();
            StopAllMusic();
            PlaySfx(sfxWin);
            WinnerText.Text = msg;
            WinScreen.Visibility = Visibility.Visible;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HUD
        // ═══════════════════════════════════════════════════════════════════
        void UpdateHUD()
        {
            P1HpBar.Value = Math.Max(0, Math.Min(100, p1HP));
            P2HpBar.Value = Math.Max(0, Math.Min(100, p2HP));
            P1HpText.Text = $"{(int)Math.Ceiling(p1HP)} / 100";
            P2HpText.Text = $"{(int)Math.Ceiling(p2HP)} / 100";
            P1HpBar.Foreground = HpColor(p1HP);
            P2HpBar.Foreground = HpColor(p2HP);
            P1KillText.Text = p1Kills.ToString();
            P2KillText.Text = p2Kills.ToString();

            int totalSec = timerFrames / 60;
            TimerText.Text = $"{totalSec / 60:D2}:{totalSec % 60:D2}";

            P1W1Ammo.Text = $"{p1Mag[0]}/{p1Reserve[0]}";
            P1W2Ammo.Text = p1Weapons[1] != WeaponType.None ? $"{p1Mag[1]}/{p1Reserve[1]}" : "—";

            P1WeaponSlot1.BorderBrush = p1ActiveSlot == 0 ? new SolidColorBrush(Colors.Yellow) : new SolidColorBrush(Color.FromRgb(70, 77, 83));
            P1WeaponSlot2.BorderBrush = p1ActiveSlot == 1 ? new SolidColorBrush(Colors.Yellow) : new SolidColorBrush(Color.FromRgb(70, 77, 83));

            P2W1Ammo.Text = $"{p2Mag[0]}/{p2Reserve[0]}";
            P2W2Ammo.Text = p2Weapons[1] != WeaponType.None ? $"{p2Mag[1]}/{p2Reserve[1]}" : "—";

            P2WeaponSlot1.BorderBrush = p2ActiveSlot == 0 ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Color.FromRgb(70, 77, 83));
            P2WeaponSlot2.BorderBrush = p2ActiveSlot == 1 ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Color.FromRgb(70, 77, 83));

            if (P1GrenadeCount != null) P1GrenadeCount.Text = $"x{p1Grenades}";
            if (P2GrenadeCount != null) P2GrenadeCount.Text = $"x{p2Grenades}";

            if (P1GrenadeSlot != null)
            {
                bool p1Ready = p1Grenades > 0 && p1GrenadeCooldown == 0;
                P1GrenadeSlot.Opacity = p1Ready ? 1.0 : 0.4;
                P1GrenadeSlot.BorderBrush = (p1GrenadeCooldown > 0)
                    ? new SolidColorBrush(Color.FromRgb(150, 60, 60))
                    : new SolidColorBrush(Color.FromRgb(70, 77, 83));
            }
            if (P2GrenadeSlot != null)
            {
                bool p2Ready = p2Grenades > 0 && p2GrenadeCooldown == 0;
                P2GrenadeSlot.Opacity = p2Ready ? 1.0 : 0.4;
                P2GrenadeSlot.BorderBrush = (p2GrenadeCooldown > 0)
                    ? new SolidColorBrush(Color.FromRgb(150, 60, 60))
                    : new SolidColorBrush(Color.FromRgb(70, 77, 83));
            }
        }

        SolidColorBrush HpColor(double hp)
        {
            if (hp > 60) return new SolidColorBrush(Color.FromRgb(76, 175, 80));
            if (hp > 30) return new SolidColorBrush(Color.FromRgb(255, 193, 7));
            return new SolidColorBrush(Color.FromRgb(239, 83, 80));
        }

        void AddKill(string msg)
        {
            killFeedLog.Insert(0, msg);
            if (killFeedLog.Count > 7) killFeedLog.RemoveAt(7);
            UpdateKillFeed();
        }

        void UpdateKillFeed()
        {
            KillFeedList.ItemsSource = null;
            KillFeedList.ItemsSource = new List<string>(killFeedLog);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  KEYBOARD
        // ═══════════════════════════════════════════════════════════════════
        void Window_KeyDown(object sender, KeyEventArgs e)
        {
            keysDown.Add(e.Key);

            if (e.Key == Key.D2) { p1ActiveSlot = p1ActiveSlot == 0 ? 1 : 0; p1ShootCooldown = 10; }
            if (e.Key == Key.O) { p2ActiveSlot = p2ActiveSlot == 0 ? 1 : 0; p2ShootCooldown = 10; }
            if (e.Key == Key.Escape) TogglePause();
        }

        void Window_KeyUp(object sender, KeyEventArgs e) => keysDown.Remove(e.Key);

        // ═══════════════════════════════════════════════════════════════════
        //  PAUSE
        // ═══════════════════════════════════════════════════════════════════
        void TogglePause()
        {
            if (p1Dead || p2Dead) return;
            isPaused = !isPaused;
            if (isPaused)
            {
                gameLoop.Stop();
                try { gameMusic.Pause(); } catch { }
            }
            else
            {
                gameLoop.Start();
                Focus();
                try { gameMusic.Play(); } catch { }
            }
        }

        void ResetHtpPages()
        {
            HtpPage1.Visibility = Visibility.Visible;
            HtpPage2.Visibility = Visibility.Collapsed;
            HtpPage3.Visibility = Visibility.Collapsed;
            HtpPageIndicator.Text = "PAGE 1 / 3";
            HtpPrevBtn.IsEnabled = false;
            HtpPrevBtn.Opacity = 0.4;
            HtpNextBtn.IsEnabled = true;
            HtpNextBtn.Opacity = 1.0;
        }

        void PlayAgain_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(sfxButtonClick);
            gameLoop?.Stop();
            InitGame();
            StartLoop();
        }

        void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(sfxButtonClick);
            MainMenuOverlay.Visibility = Visibility.Collapsed;
            Focus();
            StartLoop();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════
        void AddZ(UIElement el, double x, double y, int z)
        {
            Canvas.SetLeft(el, x); Canvas.SetTop(el, y); Canvas.SetZIndex(el, z);
            GameCanvas.Children.Add(el);
        }

        void KillBullets(List<Bullet> list)
        {
            foreach (var b in list) { GameCanvas.Children.Remove(b.Shape); bullets.Remove(b); }
        }

        void LoadHudImages()
        {
            P1AvatarImage.Source = TryLoadAsset("player1avatar.png");
            P2AvatarImage.Source = TryLoadAsset("avap2.png");
            P1Weapon1Image.Source = TryLoadAsset("Pistol.png");
            P2Weapon1Image.Source = TryLoadAsset("Pistol.png");
            P1Weapon2Image.Source = null;
            P1Weapon4Image.Source = TryLoadAsset("Grenade.png");
            P2Weapon2Image.Source = null;
            P2Weapon4Image.Source = TryLoadAsset("Grenade.png");
        }

        double Dist(double x1, double y1, double x2, double y2)
            => Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

        ImageSource TryLoadAsset(string file)
        {
            try { return new BitmapImage(new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute)); }
            catch { return null; }
        }
    }
}