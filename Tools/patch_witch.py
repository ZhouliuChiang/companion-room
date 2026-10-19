from pathlib import Path
p=Path('Assets/CompanionRoom/Scripts/Runtime/Character/RoomCompanion.cs')
s=p.read_text(encoding='utf-8-sig').replace('float yaw = Mathf.MoveTowardsAngle', 'if (GetComponent<WitchLocomotion>() != null) return;\n            float yaw = Mathf.MoveTowardsAngle');p.write_text(s,encoding='utf-8')
p=Path('Assets/CompanionRoom/Scripts/Runtime/Interaction/RoomInteractor.cs');s=p.read_text(encoding='utf-8-sig').replace('float pressTravel;', 'float pressTravel;\n        bool floorPress;')
s=s.replace('pressed = overUi ? null : PickAt(position);','pressed = overUi ? null : PickAt(position);\n                floorPress = !overUi && pressed == null;').replace('if (pressed != null && mouse.leftButton.isPressed)','if ((pressed != null || floorPress) && mouse.leftButton.isPressed)')
s=s.replace('pressed = null;\n                SetHovered(null);','pressed = null; floorPress = false;\n                SetHovered(null);')
s=s.replace('Activate(target);\n                pressed = null;', '''Activate(target);
                else if (floorPress && !overUi && target == null && pressTravel <= clickSlop)
                {
                    var walker = FindFirstObjectByType<WitchLocomotion>();
                    if (walker != null)
                    {
                        var ray = roomCamera.ScreenPointToRay(position);
                        var plane = new Plane(Vector3.up, new Vector3(0, walker.floor, 0));
                        if (plane.Raycast(ray, out float distance))
                        {
                            var point = ray.GetPoint(distance);
                            // A visible object in front of the floor consumes the click.
                            bool occluded = false;
                            foreach (var hit in Physics.RaycastAll(ray, distance - .03f, clickMask, QueryTriggerInteraction.Collide))
                                if (!hit.transform.IsChildOf(walker.transform) && hit.collider.bounds.max.y > walker.floor + .06f) occluded = true;
                            if (!occluded) walker.GoTo(point);
                        }
                    }
                }
                floorPress = false;
                pressed = null;''')
p.write_text(s,encoding='utf-8')
