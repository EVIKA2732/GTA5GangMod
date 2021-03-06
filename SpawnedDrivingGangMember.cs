﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA.Math;
using GTA.Native;

namespace GTA.GangAndTurfMod
{
    class SpawnedDrivingGangMember : UpdatedClass
    {
        public Ped watchedPed;
        public List<Ped> myPassengers = new List<Ped>();
        public Vector3 destination;
        public Vehicle vehicleIAmDriving;
        public int updatesWhileGoingToDest;
        public int updateLimitWhileGoing = 50;

        public bool playerAsDest = false;

        public override void Update()
        {
            if (vehicleIAmDriving.IsAlive && watchedPed.IsAlive)
            {
                if(destination != Vector3.Zero)
                {
                    RideToDest();
                }
                else
                {
                    //we are just wandering arond

                    //if there is a war going on and we're in the war zone, get to the player to help/kill him!
                    if (GangWarManager.instance.isOccurring && GangWarManager.instance.warZone == ZoneManager.instance.GetCurrentTurfZone())
                    {
                        playerAsDest = true;
                        updatesWhileGoingToDest = 0;
                        RideToDest();
                        return;
                    }

                    //the driver can also do some drive-by
                    if (ModOptions.instance.gangMemberAggressiveness !=
                    ModOptions.gangMemberAggressivenessMode.defensive && ModOptions.instance.fightingEnabled)
                    {
                        watchedPed.Task.FightAgainstHatedTargets(100);
                    }


                    //stop tracking this driver/vehicle if he/she leaves the vehicle
                    if (!watchedPed.IsInVehicle())
                    {
                        ClearAllRefs();
                        return;
                    }

                    //if we get too far from the player, despawn
                    if(World.GetDistance(vehicleIAmDriving.Position, Game.Player.Character.Position) > 
                            ModOptions.instance.maxDistanceCarSpawnFromPlayer * 1.5f){
                        if (!watchedPed.IsPlayer)
                        {
                            SpawnedGangMember memberAI = GangManager.instance.GetTargetMemberAI(watchedPed);
                            if (memberAI != null) memberAI.Die();
                        }
                        

                        for (int i = 0; i < myPassengers.Count; i++)
                        {
                            if (!myPassengers[i].IsPlayer)
                            {
                                SpawnedGangMember memberAI = GangManager.instance.GetTargetMemberAI(myPassengers[i]);
                                if (memberAI != null) memberAI.Die();
                            }
                            
                        }
                        ClearAllRefs();
                    }
                }
            }
            else
            {
                //our vehicle has been destroyed/ our driver has died

                for (int i = 0; i < myPassengers.Count; i++)
                {
                    if (myPassengers[i].IsAlive && myPassengers[i].IsInVehicle(vehicleIAmDriving))
                    {
                        myPassengers[i].Task.LeaveVehicle();
                    }
                }
                ClearAllRefs();
            }
            
        }

        void RideToDest()
        {

            if (playerAsDest) destination = Game.Player.Character.Position;

            //if we're close to the destination...
            if (vehicleIAmDriving.Position.DistanceTo(destination) < 25) //tweaked to match my changes below -- zix
            {
                //leave the vehicle if we wanted to get to the player and he's on foot or if we just had to get somewhere
                if (!playerAsDest || Game.Player.Character.CurrentVehicle == null)
                {
                    EveryoneLeaveVehicle();
                }

            }
            else
            {
                updatesWhileGoingToDest++;

                //give up, drop passengers and go away... but only if we're not chasing the player
                //and he/she isn't on a vehicle
                if (updatesWhileGoingToDest > updateLimitWhileGoing &&
                    (!playerAsDest || Game.Player.Character.CurrentVehicle == null))
                {
                    if (playerAsDest) //zix - extra config options
                    {
                        //if we took too long to get to the player and can't be currently seen by the player, lets just teleport close by
                        //...this should only happen with friendly vehicles, or else the player may be blitzkrieg-ed in a not funny way
                        if (!vehicleIAmDriving.IsOnScreen && ModOptions.instance.forceSpawnCars &&
                            watchedPed.RelationshipGroup == GangManager.instance.PlayerGang.relationGroupIndex)
                        {
                            vehicleIAmDriving.Position = World.GetNextPositionOnStreet(Game.Player.Character.Position, true);
                        }
                        
                    }
                    EveryoneLeaveVehicle();
                }
                else
                {
                    if (!watchedPed.IsPlayer)
                    {
                        if (playerAsDest && Game.Player.Character.CurrentVehicle != null)
                        {
                            watchedPed.Task.ClearAll();
                            Function.Call(Hash._TASK_VEHICLE_FOLLOW, watchedPed, vehicleIAmDriving, Game.Player.Character.CurrentVehicle, 4457020, 100, 20);
                        }
                        else
                        {
                            watchedPed.Task.ClearAll();
                            //watchedPed.Task.DriveTo(vehicleIAmDriving, destination, 10, 7 * Vector3.Distance(watchedPed.Position, destination), 4457020); --zix: speed is kinda ridic
                            watchedPed.Task.DriveTo(vehicleIAmDriving, destination, 25, 50, 4457020);
                        }
                    }
                }
            }
        }

        public void EveryoneLeaveVehicle()
        {
            bool someoneCanUseMountedWeapons = false;
            //leave vehicle, everyone stops being important
            if (!watchedPed.IsPlayer)
            {
                //watchedPed.Task.ClearAll();
                if(!Function.Call<bool>(Hash.CONTROL_MOUNTED_WEAPON, watchedPed))
                {
                    watchedPed.Task.LeaveVehicle();
                }
                else
                {
                    someoneCanUseMountedWeapons = true;
                }
                //watchedPed.Task.LeaveVehicle();
                //Function.Call(Hash.RESET_PED_LAST_VEHICLE, watchedPed);
                //watchedPed.MarkAsNoLongerNeeded();
            }

            for (int i = 0; i < myPassengers.Count; i++)
            {
                if (!myPassengers[i].IsPlayer)
                {
                    //myPassengers[i].MarkAsNoLongerNeeded();
                    //myPassengers[i].Task.ClearAll();
                    if(!Function.Call<bool>(Hash.CONTROL_MOUNTED_WEAPON, watchedPed))
                    {
                        myPassengers[i].Task.LeaveVehicle();
                    }
                    else
                    {
                        someoneCanUseMountedWeapons = true;
                    }
                    
                    //Function.Call(Hash.RESET_PED_LAST_VEHICLE, myPassengers[i]);
                }
            }

            if (!someoneCanUseMountedWeapons)
            {
                Function.Call(Hash.TASK_EVERYONE_LEAVE_VEHICLE, vehicleIAmDriving);
            }

            ClearAllRefs();
        }

        /// <summary>
        /// nullifies/clears references to the vehicle, driver and passengers
        /// </summary>
        public void ClearAllRefs()
        {
            if (vehicleIAmDriving != null)
            {
                if (vehicleIAmDriving.CurrentBlip != null)
                {
                    vehicleIAmDriving.CurrentBlip.Remove();
                }

                vehicleIAmDriving.IsPersistent = false;
                vehicleIAmDriving = null;
            }
            
            watchedPed = null;
            myPassengers.Clear();
        }

        public SpawnedDrivingGangMember(Ped watchedPed, Vehicle vehicleIAmDriving, Vector3 destination, bool playerAsDest = false)
        {
            this.watchedPed = watchedPed;
            this.vehicleIAmDriving = vehicleIAmDriving;
            this.destination = destination;
            this.ticksBetweenUpdates = 50;
            this.playerAsDest = playerAsDest;
        }

        /// <summary>
        /// remember who our passengers are so that we can erase them later, even if they leave the car
        /// </summary>
        public void SetWatchedPassengers()
        {
            myPassengers.Clear();
            for (int i = 0; i < vehicleIAmDriving.PassengerSeats; i++)
            {
                Ped memberInSeat = Function.Call<Ped>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicleIAmDriving, i);
                myPassengers.Add(memberInSeat);
                Function.Call(Hash.CONTROL_MOUNTED_WEAPON, memberInSeat);
            }
        }

    }
}
