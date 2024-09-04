using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static HandCtrl;

namespace KK_VR.Interpreters.Extras
{
    internal class TalkSceneExtras
    {
        internal static Dictionary<int, ReactionInfo> dicNowReactions = new Dictionary<int, ReactionInfo>
        {
            {
                0, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 0,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 1,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                1, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 0,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 1,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                2, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 2,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 3,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                3, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>
                    {
                        0
                    },
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 4,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 5,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                4, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>
                    {
                        1
                    },
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 6,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 7,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.zero,
                                    max = Vector3.up
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                5, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 8,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 9,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
            {
                6, new ReactionInfo
                {
                    isPlay = true,
                    weight = 0.3f,
                    lstReleaseEffector = new List<int>(),
                    lstParam = new List<ReactionParam>
                    {
                        new ReactionParam
                        {
                            id = 10,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        },
                        new ReactionParam
                        {
                            id = 11,
                            lstMinMax = new List<ReactionMinMax>
                            {
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = new Vector3(-1f, 0f, -1f),
                                    max = new Vector3(1f, 0f, 1f),
                                },
                                new ReactionMinMax
                                {
                                    min = Vector3.back,
                                    max = Vector3.forward
                                }
                            }
                        }
                    }
                }
            },
        };
    }
}
