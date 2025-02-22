﻿using GW2EIGW2API.GW2API;

[assembly: System.CLSCompliant(false)]
namespace GW2EIGW2API
{
    public class GW2APIController
    {
        private readonly GW2SkillAPIController skillAPIController = new GW2SkillAPIController();
        private readonly GW2SpecAPIController specAPIController = new GW2SpecAPIController();
        private readonly GW2TraitAPIController traitAPIController = new GW2TraitAPIController();
        /// <summary>
        /// API Cache init with a cache file locations, 
        /// If the files are present, the content will be used to initialize the API caches
        /// Otherwise the caches will be built from GW2 API calls
        /// </summary>
        /// <param name="skillLocation"></param>
        /// <param name="specLocation"></param>
        /// <param name="traitLocation"></param>
        public GW2APIController(string skillLocation, string specLocation, string traitLocation)
        {
            skillAPIController.GetAPISkills(skillLocation);
            specAPIController.GetAPISpecs(specLocation);
            //traitAPIController.GetAPITraits(traitLocation);
        }

        /// <summary>
        /// Cacheless API initialization
        /// </summary>
        public GW2APIController()
        {
            skillAPIController.GetAPISkills(null);
            specAPIController.GetAPISpecs(null);
            //traitAPIController.GetAPITraits(null);
        }

        //----------------------------------------------------------------------------- SKILLS

        /// <summary>
        /// Returns GW2APISkill item
        /// Warning: this method is not thread safe, 
        /// Make sure to initialize the cache before hand if you intend to call this method from different threads
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public GW2APISkill GetAPISkill(long id)
        {
            return skillAPIController.GetAPISkills(null).Items.TryGetValue(id, out GW2APISkill skill) ? skill : null;
        }

        public void WriteAPISkillsToFile(string filePath)
        {
            skillAPIController.WriteAPISkillsToFile(filePath);
        }

        //----------------------------------------------------------------------------- SPECS
        /// <summary>
        /// Returns GW2APISpec item
        /// Warning: this method is not thread safe, 
        /// Make sure to initialize the cache before hand if you intend to call this method from different threads
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public GW2APISpec GetAPISpec(int id)
        {
            return specAPIController.GetAPISpecs(null).Items.TryGetValue(id, out GW2APISpec spec) ? spec : null;
        }

        public void WriteAPISpecsToFile(string filePath)
        {
            specAPIController.WriteAPISpecsToFile(filePath);
        }

        //----------------------------------------------------------------------------- TRAITS


        /// <summary>
        /// Returns GW2APITrait item
        /// Warning: this method is not thread safe, 
        /// Make sure to initialize the cache before hand if you intend to call this method from different threads
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public GW2APITrait GetAPITrait(long id)
        {
            return traitAPIController.GetAPITraits(null).Items.TryGetValue(id, out GW2APITrait trait) ? trait : null;
        }
        public void WriteAPITraitsToFile(string filePath)
        {
            traitAPIController.WriteAPITraitsToFile(filePath);
        }

    }
}

