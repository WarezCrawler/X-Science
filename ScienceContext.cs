using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // For Collider
using System.Threading;

// The current state of science in KSP
namespace ScienceChecklist
{
    public sealed class ScienceContext
    {
        private readonly Logger _logger;
        private readonly ScienceChecklistAddon _parent;
        private Dictionary<CelestialBody, Body> _bodyList;
        private Dictionary<string, List<ScienceData>> _onboardScience;
        private List<ScienceData> _currentVesselScience = new List<ScienceData>();
        private Dictionary<string, ScienceSubject> _scienceSubjects;
        private Dictionary<ScienceExperiment, ModuleScienceExperiment> _experiments;
        private static IList<string> _kscBiomes = new List<string>();           //WarezCrawler changed to static
        private static CelestialBody _homeWorld;                                //WarezCrawler changed to static
        private static string _kscBiome;                                        //WarezCrawler changed to static
        private UnlockedInstrumentList _unlockedInstruments;
        private List<ScienceInstance> _allScienceInstances;


        // Gets all experiments that are available in the game.
        public IList<ScienceInstance> AllScienceInstances { get { return _allScienceInstances; } }

        public Dictionary<CelestialBody, Body> BodyList { get { return _bodyList; } }
        public Dictionary<string, List<ScienceData>> OnboardScienceList { get { return _onboardScience; } }
        public List<ScienceData> CurrentVesselScience { get { return _currentVesselScience; } }
        public Dictionary<string, ScienceSubject> ScienceSubjects { get { return _scienceSubjects; } }
        public Dictionary<ScienceExperiment, ModuleScienceExperiment> Experiments { get { return _experiments; } }
        public IList<string> KscBiomes { get { return _kscBiomes; } }
        public CelestialBody HomeWorld { get { return _homeWorld; } }
        public string KscBiome { get { return _kscBiome; } }
        public UnlockedInstrumentList UnlockedInstruments { get { return _unlockedInstruments; } }



        public ScienceContext(ScienceChecklistAddon Parent)
        {
            _parent = Parent;
            _logger = new Logger(this);
            //_logger.Trace( "Making ScienceContext" );
            _bodyList = new Dictionary<CelestialBody, Body>();
            //_logger.Trace( "Made _bodyList" );
            _onboardScience = new Dictionary<string, List<ScienceData>>();
            //_logger.Trace( "Made _onboardScience" );
            _scienceSubjects = new Dictionary<string, ScienceSubject>();
            //_logger.Trace( "Made _scienceSubjects" );
            _experiments = new Dictionary<ScienceExperiment, ModuleScienceExperiment>();
            //_logger.Trace( "Made _experiments" );
            _kscBiomes = new List<string>();
            //_logger.Trace( "Made _kscBiomes" );
            _unlockedInstruments = new UnlockedInstrumentList();
            //_logger.Trace( "Made _unlockedInstruments" );
            _allScienceInstances = new List<ScienceInstance>();
            //_logger.Trace( "Made _allScienceInstances" );

            Reset();
            //_logger.Trace( "Made ScienceContext" );

            //workerThreads.Add(new Thread(() => { if (_bodyList.Count != FlightGlobals.Bodies.Count) UpdateBodies(); } ));
            //workerThreads.Add(new Thread(UpdateOnboardScience));
            //workerThreads.Add(new Thread(UpdateScienceSubjects));
            //workerThreads.Add(new Thread(UpdateExperiments));
            //workerThreads.Add(new Thread(UpdateKscBiomes));
            //workerThreads.Add(new Thread(RefreshExperimentCache));

            //workerThreads.Add(new Thread(() => 
            //{
            //    if (_bodyList.Count != FlightGlobals.Bodies.Count)
            //        UpdateBodies();
            //    UpdateOnboardScience();
            //}));
            //workerThreads.Add(new Thread(() =>
            //{
            //    UpdateScienceSubjects();
            //    UpdateExperiments();
            //    UpdateKscBiomes();
            //    RefreshExperimentCache();
            //}));


            //for (int i = 0; i < workerThreads.Count; i++)
            //{
            //    workerThreads[i].IsBackground = true;
            //    workerThreads[i].Priority = System.Threading.ThreadPriority.Normal;
            //}
        }


        //List<Thread> workerThreads = new List<Thread>(2);
        public void Reset()
        {
            if (ResearchAndDevelopment.Instance == null)
                return;
            if (PartLoader.Instance == null)
                return;

            //WarezCrawler 2018-04-03 added check for if the bodies are already collected in the dictionary
            if (_bodyList.Count != FlightGlobals.Bodies.Count)
                UpdateBodies();
            UpdateOnboardScience();
            UpdateScienceSubjects();
            UpdateExperiments();
            UpdateKscBiomes();
            RefreshExperimentCache();

            //for (int i = 0; i < workerThreads.Count; i++)
            //    workerThreads[i].Start();

            //for (int i = 0; i < workerThreads.Count; i++)
            //    workerThreads[i].Join();
        }



        private void UpdateBodies()
        {
            var bodies = FlightGlobals.Bodies;



            // Handle added and updated bodies
            for (int x = 0; x < bodies.Count; x++)
            {
                //String s = String.Format( "Body {0} - {1}.", bodies[ x ].flightGlobalsIndex, bodies[ x ].name );
                //_logger.Trace( s );
                if (!_bodyList.ContainsKey(bodies[x]))
                {
                    var B = new Body(bodies[x]);
                    _bodyList.Add(bodies[x], B);
                }
                else
                    _bodyList[bodies[x]].Update();
            }



            // Handle deleted bodies
            foreach (var CC in _bodyList)
            {
                if (!bodies.Contains(CC.Key))
                {
                    _bodyList.Remove(CC.Key);
                }
            }
        }



        List<ScienceData> onboardScience = new List<ScienceData>();
        List<ScienceData> onboardScience_unloadedVessels = new List<ScienceData>();     //WarezCrawler
        List<string> vesselIds = new List<string>();
        IEnumerable<Vessel> LoadedVessels;
        List<ScienceData> D = new List<ScienceData>();
        ConfigNode CurrentFlightStateNode = new ConfigNode();

        // Gets all available onboard science.
        private void UpdateOnboardScience()
        {
            // Init
            //				var Start = DateTime.Now;
            //				TimeSpan Elapsed;

            //var onboardScience = new List<ScienceData>();
            //var vesselIds = new List<string>();
            onboardScience.Clear();
            vesselIds.Clear();

            //_currentVesselScience = new List<ScienceData>();
            _currentVesselScience.Clear();


            // Handle loaded craft, remember the Ids so we can filter the unloaded ones
            LoadedVessels = FlightGlobals.Vessels.Where(x => x.loaded);
            D.Clear();
            foreach (var v in LoadedVessels)
            {
                if (_parent.Config.CheckDebris || v.vesselType != VesselType.Debris)
                {
                    D = v.FindPartModulesImplementing<IScienceDataContainer>()
                       .SelectMany(y => y.GetData() ?? new ScienceData[0]).ToList();
                    vesselIds.Add(v.id.ToString().ToLower().Replace("-", ""));

                    onboardScience.AddRange(D);
                }

                if (FlightGlobals.ActiveVessel)
                {
                    if (FlightGlobals.ActiveVessel == v)
                    {
                        _currentVesselScience = D;
                    }
                }
            }
            //Debug.Log("[XS-GTI] loaded vessel science -- " + onboardScience.Count());
            //WarezCrawler - Don't check unloaded vessels more than the once.
            if (LoadStatus.OnboardScienceUnloadedVessels && onboardScience_unloadedVessels.Count != 0)     //ExperimentCacheLoaded is reset on every scene change => reload
            {
                onboardScience.AddRange(onboardScience_unloadedVessels);
                //Debug.Log("[XS-GTI] applying existing unloaded vessel science -- " + onboardScience_unloadedVessels.Count());
            }
            else
            {
                //Debug.Log("[XS-GTI] Reevaluating unloaded vessel science");
                //onboardScience_unloadedVessels

                // Look for science in unloaded vessels.
                // Don't do debris or already loaded vessels(from routine above)
                // I was having execptions because something was NULL.
                // Only happend on a brand-new game, not a load.
                // This seemed to fix it
                if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.flightState != null)
                {
                    // Dump all the vessels to a save.
                    //var node = new ConfigNode();
                    CurrentFlightStateNode.ClearData();     //Reset previous CurrentFlightStateNode
                    HighLogic.CurrentGame.flightState.Save(CurrentFlightStateNode);
                    if (CurrentFlightStateNode == null)
                        _logger.Trace("flightState save is null");
                    else
                    {
                        onboardScience_unloadedVessels.Clear();     //WarezCrawler - Clear the unloaded vessel rep for update
                                                                    // Grab the unloaded vessels
                        ConfigNode[] vessels = CurrentFlightStateNode.GetNodes("VESSEL");
                        onboardScience_unloadedVessels.AddRange
                        (
                            vessels.Where(x => _parent.Config.CheckDebris || x.GetValue("type") != "Debris")
                                .Where(x => !vesselIds.Contains(x.GetValue("pid"))) // Not the active ones, we have them already
                                    .SelectMany(x => x.GetNodes("PART")
                                       .SelectMany(y => y.GetNodes("MODULE")
                                          .SelectMany(z => z.GetNodes("ScienceData")).Select(z => new ScienceData(z))
                                       )
                                    )
                        );
                        //Debug.Log("[XS-GTI] applying new unloaded vessel science -- " + onboardScience_unloadedVessels.Count());
                        onboardScience.AddRange(onboardScience_unloadedVessels);        //WarezCrawler - add the unloaded sciences to the list
                    }
                }
                LoadStatus.OnboardScienceUnloadedVessels = true;
            }
            //Debug.Log("[XS-GTI] total science count -- " + onboardScience.Count());

            // Turn all the science into a dictionary
            //##Dictionary<string, List<ScienceData>> onboardScienceDict = new Dictionary<string, List<ScienceData>>();
            _onboardScience.Clear();
            for (var x = 0; x < onboardScience.Count; x++)
            {
                if (!_onboardScience.ContainsKey(onboardScience[x].subjectID))
                    _onboardScience.Add(onboardScience[x].subjectID, new List<ScienceData>());
                _onboardScience[onboardScience[x].subjectID].Add(onboardScience[x]);
            }

            // Update the dictionary
            //				Elapsed = DateTime.Now - Start;
            //_logger.Trace( "GetOnboardScience took " + Elapsed.ToString( ) + "ms and found " + onboardScience.Count( ) + " ScienceData" );
            //##_onboardScience = onboardScienceDict;
        }


        List<ScienceSubject> SciSubjects;
        //internal static bool updateScienceSubjects = true;
        //List<ScienceSubject> tmpSciSubjects;
        List<ScienceSubject> EmptySubjects = new List<ScienceSubject>(1);
        private void UpdateScienceSubjects()
        {
            Debug.Log("[XS-GTI] UpdateScienceSubjects() and updateScienceSubjects = " + LoadStatus.ScienceSubjects);
            if (!LoadStatus.ScienceSubjects || _scienceSubjects.Count == 0)
            {
                if (ResearchAndDevelopment.GetSubjects() == null)
                    Debug.Log("[XS-GTI] ResearchAndDevelopment.GetSubjects() is null");

                if (_scienceSubjects.Count == 0)
                    Debug.Log("[XS-GTI] _scienceSubjects.Count == 0");

                SciSubjects = (ResearchAndDevelopment.GetSubjects() ?? EmptySubjects);
                _scienceSubjects = SciSubjects.ToDictionary(p => p.id);
                LoadStatus.ScienceSubjects = true;
            }




            //Original code
            //var SciSubjects = (ResearchAndDevelopment.GetSubjects() ?? new List<ScienceSubject>());
            //Dictionary<string, ScienceSubject> SciDict = SciSubjects.ToDictionary(p => p.id);
            //_scienceSubjects = SciDict;
        }



        private void UpdateExperiments()
        {
            //			var StartTime = DateTime.Now;
            if (LoadStatus.Experiments)
                return;

            _experiments.Clear();
            for (int x = 0; x < PartLoader.LoadedPartsList.Count; x++)
            {
                AvailablePart P = PartLoader.LoadedPartsList[x];
                List<ModuleScienceExperiment> Modules = P.partPrefab.FindModulesImplementing<ModuleScienceExperiment>();
                for (int y = 0; y < Modules.Count; y++)
                {
                    ModuleScienceExperiment Module = Modules[y];
                    if (Module != null)
                    {
                        if (Module.experimentID != null)
                        {
                            ScienceExperiment Experiment = ResearchAndDevelopment.GetExperiment(Module.experimentID);
                            if (Experiment != null)
                            {
                                if (!_experiments.ContainsKey(Experiment))
                                    _experiments.Add(Experiment, Module);
                            }
                        }
                    }
                }
            }
            LoadStatus.Experiments = true;


            //_logger.Trace( "_experiments contains " + _experiments.Count.ToString( ) + " items" );
            //var Elapsed = DateTime.Now - StartTime;
            //_logger.Trace( "UpdateExperiments Done - " + Elapsed.ToString( ) + "ms" );
        }



        private void UpdateKscBiomes()
        {
            //var StartTime = DateTime.Now;
            //if (LoadStatus.KSCBiomes)
            //    return;
            if (_homeWorld == null)
                _homeWorld = FlightGlobals.GetHomeBody();

            if (!LoadStatus.KSCBiomes && SpaceCenter.Instance != null)
            {
                _kscBiome = null;
                var lat = SpaceCenter.Instance.Latitude;
                var lng = SpaceCenter.Instance.Longitude;
                _kscBiome = ScienceUtil.GetExperimentBiome(_homeWorld, lat, lng);
                //_logger.Trace( "KSC is in the " + biome + " biome" );

                LoadStatus.KSCBiomes = true;
            }

            // Find the KSC baby biomes
            // This is throwing exceptions.  I think the callback is being thrown before the world is finished updating.
            if (_homeWorld != null)
            {
                _kscBiomes.Clear();
                _kscBiomes = UnityEngine.Object.FindObjectsOfType<Collider>()
                    .Where(x => x.gameObject.layer == 15)
                    .Select(x => x.gameObject.tag)
                    .Where(x => x != "Untagged")
                    .Where(x => !x.Contains("KSC_Runway_Light"))
                    .Where(x => !x.Contains("KSC_Pad_Flag_Pole"))
                    .Where(x => !x.Contains("Ladder"))
                    .Select(x => Vessel.GetLandedAtString(x))
                    .Select(x => x.Replace(" ", ""))
                    .Distinct()
                    .ToList();
            }
            //LoadStatus.KSCBiomes = true;
            /*_logger.Trace( "_kscBiomes contains " + _kscBiomes.Count.ToString( ) + " items" );
            var Elapsed = DateTime.Now - StartTime;
            _logger.Trace( "UpdateKscBiomes Done - " + Elapsed.ToString( ) + "ms" );*/
        }

        //WarezCrawler 2018-04-03
        BodySituationFilter BodyFilter = new BodySituationFilter();
        //internal static bool ExperimentCacheLoaded = false;

        internal static LoadStatusType LoadStatus;

        /// <summary>
        /// Refreshes the experiment cache. THIS IS VERY EXPENSIVE.
        /// CB: Actually doesn't seem much worse than UpdateExperiments()
        /// </summary>
        private void RefreshExperimentCache()
        {
            Debug.Log("[XS-GTI] RefreshExperimentCache()");
            if (_allScienceInstances != null)
                Debug.Log("[XS-GTI] _allScienceInstances.Count: " + _allScienceInstances.Count);
            // Init
            //var StartTime = DateTime.Now;
            //BodySituationFilter BodyFilter = new BodySituationFilter( );
            _unlockedInstruments.Clear();
            //WarezCrawler 2018-04-03
            if (!LoadStatus.ExperimentCache || !HighLogic.LoadedSceneIsFlight)
            {
                Debug.Log("[XS-GTI] Clearing experiments");
                _allScienceInstances.Clear();
                LoadStatus.ExperimentCache = false;
            }


            //_logger.Info( "RefreshExperimentCache" );


            // Quick check for things we depend on
            if (ResearchAndDevelopment.Instance == null || PartLoader.Instance == null)
            {
                _logger.Debug("ResearchAndDevelopment and PartLoader must be instantiated.");
                return;
            }


            if (!LoadStatus.ExperimentCache)
            {
                Debug.Log("[XS-GTI] Updating _allScienceInstances");
                if (_allScienceInstances != null)
                    Debug.Log("[XS-GTI] _allScienceInstances.Count: " + _allScienceInstances.Count);
                // Check this experiment in all biomes on all bodies

                // Loop around all experiments
                foreach (var X in _experiments)
                {
                    var experiment = X.Key;


                    //_logger.Trace( experiment.experimentTitle );
                    // Where the experiment is possible
                    uint sitMask = experiment.situationMask;
                    uint biomeMask = experiment.biomeMask;



                    /* Need to look at
                        public bool BiomeIsRelevantWhile( ExperimentSituations situation );
                        public bool IsAvailableWhile( ExperimentSituations situation, CelestialBody body );
                    On ScienceExperiment
                    */



                    // OrbitalScience support - where the experiment is possible
                    if (sitMask == 0 && _experiments[experiment] != null)
                    {
                        var sitMaskField = _experiments[experiment].GetType().GetField("sitMask");
                        if (sitMaskField != null)
                        {
                            sitMask = (uint)(int)sitMaskField.GetValue(_experiments[experiment]);
                            //_logger.Trace( "Setting sitMask to " + sitMask + " for " + experiment.experimentTitle );
                        }

                        if (biomeMask == 0)
                        {
                            var biomeMaskField = _experiments[experiment].GetType().GetField("bioMask");
                            if (biomeMaskField != null)
                            {
                                biomeMask = (uint)(int)biomeMaskField.GetValue(_experiments[experiment]);
                                //_logger.Trace( "Setting biomeMask to " + biomeMask + " for " + experiment.experimentTitle );
                            }
                        }
                    }



                    List<ExperimentSituations> SituationList = Enum.GetValues(typeof(ExperimentSituations)).Cast<ExperimentSituations>().ToList<ExperimentSituations>();
                    List<Body> bodies = new List<Body>(_bodyList.Values.ToList());



                    // Check for CelestialBodyFilter
                    if (_experiments[experiment] != null)
                    {
                        //_logger.Trace( Experiments[ experiment ].experimentID );
                        if (CelestialBodyFilters.Filters.HasValue(_experiments[experiment].experimentID))
                        {
                            string FilterText = CelestialBodyFilters.Filters.GetValue(_experiments[experiment].experimentID);
                            BodyFilter.Filter(bodies, SituationList, FilterText);
                        }
                    }

                    for (int body_index = 0; body_index < bodies.Count; body_index++)
                    {
                        //WarezCrawler 2018-04-03
                        //if (HighLogic.LoadedSceneIsFlight && (FlightGlobals.ActiveVessel != null && FlightGlobals.currentMainBody != null))
                        //    if (bodies[body_index].CelestialBody != FlightGlobals.currentMainBody)
                        //        continue;

                        if (experiment.requireAtmosphere && !bodies[body_index].HasAtmosphere)
                            continue; // If the whole planet doesn't have an atmosphere, then there's not much point continuing.
                        for (int situation_index = 0; situation_index < SituationList.Count; situation_index++)
                        {
                            if (SituationList[situation_index] == ExperimentSituations.SrfSplashed && !bodies[body_index].HasOcean)
                                continue; // Some planets don't have an ocean for us to be splashed down in.

                            if (SituationList[situation_index] == ExperimentSituations.SrfLanded && !bodies[body_index].HasSurface)
                                continue; // Jool and the Sun don't have a surface.

                            if ((SituationList[situation_index] == ExperimentSituations.FlyingHigh || SituationList[situation_index] == ExperimentSituations.FlyingLow) && !bodies[body_index].HasAtmosphere)
                                continue; // Some planets don't have an atmosphere for us to fly in.

                            if ((sitMask & (uint)SituationList[situation_index]) == 0)
                                continue; // This experiment isn't valid for our current situation.

                            if (bodies[body_index].Biomes.Any() && (biomeMask & (uint)SituationList[situation_index]) != 0)
                            {
                                for (int biome_index = 0; biome_index < bodies[body_index].Biomes.Count(); biome_index++)
                                {
                                    ScienceInstance S = new ScienceInstance(experiment, new Situation(bodies[body_index], SituationList[situation_index], bodies[body_index].Biomes[biome_index]), this);
                                    if (BodyFilter.TextFilter(S))
                                        if (!_parent.Config.FilterDifficultScience || BodyFilter.DifficultScienceFilter(S))
                                            _allScienceInstances.Add(S);
                                }
                            }
                            else
                            {
                                ScienceInstance S = new ScienceInstance(experiment, new Situation(bodies[body_index], SituationList[situation_index]), this);
                                if (BodyFilter.TextFilter(S))
                                    if (!_parent.Config.FilterDifficultScience || BodyFilter.DifficultScienceFilter(S))
                                        _allScienceInstances.Add(S);
                            }
                        }
                    }

                    if (((sitMask & (uint)ExperimentSituations.SrfLanded) != 0) && ((biomeMask & (uint)ExperimentSituations.SrfLanded) != 0))
                    {
                        if (_homeWorld != null && _kscBiomes.Count > 0)
                        {
                            if (bodies.Contains(_bodyList[_homeWorld])) // If we haven't filtered it out
                            {
                                if (SituationList.Contains(ExperimentSituations.SrfLanded))
                                {
                                    //_logger.Trace( "BabyBiomes " + experiment.experimentTitle + ": " + sitMask );
                                    for (int x = 0; x < _kscBiomes.Count; x++)
                                    {
                                        ScienceInstance S = new ScienceInstance(experiment, new Situation(_bodyList[_homeWorld], ExperimentSituations.SrfLanded, _kscBiome, _kscBiomes[x]), this);
                                        if (BodyFilter.TextFilter(S))
                                            if (!_parent.Config.FilterDifficultScience || BodyFilter.DifficultScienceFilter(S))
                                                _allScienceInstances.Add(S);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //WarezCrawler 2018-04-03
            if (_allScienceInstances != null)
                LoadStatus.ExperimentCache = true;

            if (_allScienceInstances != null)
                Debug.Log("[XS-GTI] Post Count _allScienceInstances: " + _allScienceInstances.Count);



            //			var Elapsed = DateTime.Now - StartTime;
            //			_logger.Trace( "RefreshExperimentCache Done - " + Elapsed.ToString( ) + "ms" );
        }



        /// <summary>
        /// Calls the Update method on all experiments.
        /// </summary>
        public void UpdateAllScienceInstances()
        {
            //			var StartTime = DateTime.Now;
            //updateScienceSubjects = true;
            Debug.Log("[XS-GTI] UpdateAllScienceInstances()");
            UpdateScienceSubjects();
            UpdateOnboardScience();
            for (int x = 0; x < _allScienceInstances.Count; x++)
            {
                _allScienceInstances[x].Update(this);
            }
            //			var Elapsed = DateTime.Now - StartTime;
            //			_logger.Trace( "UpdateExperiments Done - " + Elapsed.ToString( ) + "ms" );
        }
    }

    //Struct for update states
    internal struct LoadStatusType
    {
        internal bool Experiments;
        internal bool ExperimentCache;
        internal bool ScienceSubjects;
        internal bool KSCBiomes;
        internal bool OnboardScienceUnloadedVessels;
    }
}
