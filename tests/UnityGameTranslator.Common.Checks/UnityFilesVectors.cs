namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// LZMA streams of records alike (as a font's tables are), written by Python's lzma module (liblzma,
    /// preset 9): they reach the coder's repeats after matches, which text and noise do not. The records
    /// are rebuilt by <see cref="UnityFilesChecks.Records"/> from the same generator.
    /// </summary>
    internal static class UnityFilesVectors
    {
        internal const string RecordsLc3 =
            "XQAAAAQAAGAuf+yPq3z626wiiNeaTAhwrZjZYedpvyAWrfVT17NLFWLlghVfK69TPVWXNmIo01wiaUE4i1MCYvptU/BBXXQd9jn4" +
            "IpO2jiUHOdoai0pYteIluwnjqQMmjsyY08VDWWyK5Cy/rBbryXkT7z9beoezJuLtQ/LMpFJoKDW3VT5Qfn+v0HbDVRFujBlS1fUF" +
            "Vo2WEZuL2YNSLaV38YhefWhMFObmVecwcieGqSCQmBcgrqBL82ItmpMubSn0Pa1TuHnCIW94dlUcpZzT2SMhBM7TqyzaY276wKw/" +
            "hZOrfm2dvwbDZiNEODEDwCYbmtZfBcbJeXWroMkmYJRZycInnmEr7wVXx0HW1keynFp0kw62a4KMTPjhpfnoaDoxsrGTf1mSaM2B" +
            "MjlCjCf5TstME0B57XUMF/0atnaboHqE2vq8g2b+G74XAOI3TEJzgHOGWBPTg7Ads3Qa+vZWv8w5LE6JAF3y90hqIfXHTAa7F5um" +
            "iOlwjfHISSMgQ8bo3YmpWQNT0FNen1gCohWA6Jnw0n4g3E7lWaqah9DGaNAz4rP+Riv2Hk93vug4e6kyLk/xz+5wnk9JSS1yr/E8" +
            "0ZM0IQznDYYkeK+JwUKcvUlmEKKoBIFcEyKFwRLYarE0Cq1WEEaXvjlmpRCXGiIyK0eUdR7dt8Z0fOMS4d1ndcC/qjAtpw/orS3E" +
            "BxswCq7BIMmfYQ4GsrHi4zJoLeQM7FgUrWJgvfIo5NG291YDrvM1QCe4vhtJpDhgdvj1zqCIiY8vr2hYBFgJ4lxmBAtAfSu7Sqyx" +
            "NH9A1gdVKAieW1b0kwoye8Nsh66rnI4H07AsswR3vXI++fnVbmcaEv0nFa2rdD/F3DiFs9FKrPS8c7tsgIjT59eqdimwJ5gyR16/" +
            "CqwBK++A6CMEBrSkTt0vlqpFxlVc9uANPaFDZ8qKnFT3u7ORTNBSw55FBuplpHXK2hxQutZS8SygqEUa6Ls+EgQ7+Rh6NahXVUZn" +
            "RveCZzM5j4P851iqtR/0Y3ch4IvzroQsL1bGEuhJ/KfccOdDn2Wcy1kr3sC9VHU5Xm3Xrqk/qMBaeKuWxjguFubl/Yiwd649NkVk" +
            "2t8IezP5yEhRDt1+43OUSzAgTxAE2GtcQIWph54nW9Dv13AtauwXudKSOY3vDLTXQQr7fRVjeuZTEkoVQxYvXB+weZyl+GYFCLK8" +
            "5Cf//unXreIl8Gsi/sC9s52frUTkHfyAk9FnkB9zDlGfTpgcPvx0zDyMGWXBAkGxtcK39j3Hh5EhuP3bMbIhTlJ4yoa3J82GAAVi" +
            "RTmSzn8LjQ87i/wUwVLJcxRKerVBQ5lSvaEQzSE90nbDS5Yfi5z3RO+wYioWNeHry61L3N/XxJIh2KguTEP413ltYFfE39zK4VwM" +
            "syONh1HW5gIivXpCD2I2VfVkdIz4S2GH7shN8QLduyQpC7AHp8bJY3fOAEBIAcTEw74EhkiLrPigjbnuzV6d5Sdnt/I0dk4uJ0UO" +
            "7V7WQsjQLK5BoaNreayZhO8R67VTc3mk624q7UFd0z5kaxVkFwagXyoR7TzWjGpSsFJ7CzmGL9pX6y22sb61r3ppalpMtwQNuQOk" +
            "UWDhNsrsBsrQ4hKLZ1r+BvMSOf1dYqPDvwZicZF3gHYflzHczGrWpVlia3aNZFC9J2sOJmOWNasefB1OAJO/i4tjeaLdF584Sk3D" +
            "YGw5bb48xRRz3cR4bu0Txk9aBuclTcQeYP16cEkfCZjYoGR4V36WGKw3TQ8Dm7nsTFFuB3skL9MncPIXU6mQB1BYqVCvkOWxhb71" +
            "xqvvb4Km/QCCZkn2j2oRKztWI2tVxDXhaugYIPnD7R+JQSTUErFNQqyB7Kb7xtkpNq+SKHoC9vxNon6ZVlKIKRHLXeNaeePjBqvm" +
            "vfRaQxPDUSxENf5o1+Y5J3miUTvCQaYVWlNnh15o+LtG+KFHdvqA9iEZwVCLrmwDS5yvWGQrbG+Ic743ommRtTL19TM3X1Zs0H5V" +
            "YyP29W1pCt6Lxj3NaKWxQaH+7Sj8XsSrzQFMd6fSbrmTLnFBlA48hyjk9iq0EDOKcZ1+vmKfPQy7GY1iGVcLh1SnG7jEa76OIM0M" +
            "RIeN2lHtLAl58gJLYMZrX82qHN1mV8qLB1C8nsZTcQOhzoXfK4m+GBtyUBBmfQOAJbWDv5vkfQOHcPSxt6A6LFiPNH73BwloiJoV" +
            "rinDvAsN2QKKegAKarL81ajx7CtN+4SmiFQACGFM9W4uZM6YUc1bNoLjh59Wa43ui2tX6aH10PElQ+B+u9n0cuC7fibmme2cvBLV" +
            "m8YLfu8+QD4fbdC1Z9XoyKWiaB/9RA/s0wohZY75TlAdvukAOCDwaX31cuIAsLIi2l/YvHcF8+2l3ju1k6IPiP/XLJ+Z4+dLA9lH" +
            "Ds1x2pTYx0bCzGkG7pSWWV98KURBVkwmxfb83GOAug/TGYto4+KW1qj8jtS4qqS6w6qiLYiSmQI2AUoIVAyuIeuLWNoMpsx6iqDn" +
            "09Vd3EkguaI6Z+7zgFGpuGqATBT7oq07XCidau7+uRG2PW+0jSsjlAOQIvvQsw85AVhwD0sjLuNCmVbfxoyPuB6Mb9ku70GBHpZq" +
            "LevI4N0KolVzPE2MGDNA6OM5mRsk0gsu3HFRSQm4h8ObbVz9zeixoIxzmQgMks54wH3GQMk0q3n8FBy5z+7ZaZh1HCFF2Ugfcu/3" +
            "dDuKwiL9BmfnexL/Ze+Z3hdKNG5zUaLjGBxewFGhhJwtPvx/YG3QoRQXXzwGoLAkfGqtalbpeWMBhlZf5VT6LqmuO7DO22dSWZZx" +
            "e2c94VnXvPueC4zTTg4PT8Xh8H/byl6R/J+thHjA7PkYFonQ5zUROh/ZAWt5yaREDN4YRhKKhsRX9lTCyVM/69yk1uODDFGMJaOe" +
            "1vvYMk1Wey5p2gt6Ft8z6fj1jxzkQx+NcEIxfvlurcTnZSHCtApJ1fORP/AjFw5t5ii2CSS4OcdHQTebmBMwiIcXG7F2rtbtfvbg" +
            "JFlYj9L/knnuXYDSDGcM/lYz9Ryy2t5WzbR5OUsXfT50xPvJLdHUbND/+e6VfGBaIJiVYzHCQoV+DXl8zC21DGovtQ4wwCmNwt7x" +
            "rgVKFduUGkwpYa5UfYPq6Ob3qACD4+I9UTibfHUKro1BfB2d091HNWqRql5bt5N2mv7qFJ/hGSIoFzh/YNn9Nb72ufXM0zGj2Dvp" +
            "iTdRpVP1j19+iAVlhVkOsYqHkfXvDuFGhxkZnBjhS6pMPtfAW8sW4x4vwpn3/gweq7jSlNALfOPa8DX8eDC7i38Q+5rbXj6Na1vY" +
            "JcmJDv1zvsUs1qv3jsJqkKrKgYJgbCJtqx5lbGADm4+j1V2UjAZrhfaNOpJ/IPCBNeV8L4sJhFVxaLXARuuqWk9OAxVlfW+eqqiF" +
            "w6g3cUtl27YMhq9Vi1yOI2G5rjLpiIGPEWRmYG235ML8fDW8X6Mvx/XPntNY8vkyI2noKinOLluham+c9JY2a2k97YXSQ9hcOfM+" +
            "RUypsXtAUNSk56/LqWtRdJNtQlUMq9cmeRaPNdi2dWpQ2tHBG/8J1CYdTi9hRQDX69gj6MZQ5b7xJUwZhwprBVNUxq+fqME5+wGq" +
            "O1DAia4ZreRxzyPFNmV8XwXN9PkyU6xSV7crUyRrE78ApqWVZTAKkxkwmjXqabkFFcjh8fH7jB0MbWApkB5Mc2Fjw+/eK45nxv2J" +
            "akKYzcDJYq3WFyS01P9bS4Oai4ah3XozzWbSJvfGyNzR0VM8VwKFsQXrvPXcPG+XuqwvqgP8Xl6NfaUumYAfva/kaHKTLGH40Gzm" +
            "ZmDIDvU2CdOvl28o3RW4stln5XZ8WEte1wxTl+H1XWpVY46Mcdn3zQxysp0Kx3NJry7fK9b2G/XeiiG7vMNA/KcxLG2su9PoYOfB" +
            "ddCs5P5wlmkPxX8Dutvevz2H3jyv9ysf2kQvYRzjNOReLuCEOJt36clPSp+K4owaGpzbttlvtZ6gopFz5MzTi0s2AYeK+Aj16mir" +
            "eapArQVfFLSet2CIGgtBzlTvIsUi2DwkWWnB3gOUSE4hzCUu+5idotkSmMenXYgECCG7XEggDWE1SBq6kUc3DpK8qkxostE45ZG1" +
            "R/HwNWJy0bir7VFrZKuIyIuL8JgHqM8cxMvwy3QKNMa475/KV+/SBvBbJdGX948b4n2ZaTfJHezuhSLsKCAmb+zBfnPrAqp62atZ" +
            "ZhtI/WNJNta5NT1mFxCO3Dj9fZGmhXqCEKsNseZrYpEaohODz1gb/svHWEudvNgFwqbAB+UvebqfRaeeqIibQVnTX3ib2XRoyVT0" +
            "BeG1pcQsM6yUblhom2YndwFWrbvJ8HMLYrTIl6oeQYIBRPIf8EGlKJnY35xKpv5DjCTx8cqBsvRt625M/hsF9gr4wEqt1iXp2ek+" +
            "riwAeZ7Wk3tiDCuFDzAwQ512vECOlFNZYS5QloXwNKaXz7M+u/UmIe+rz3voZN8AgVOB56o+w9agZpDnQCG3CqbMXXAeJiCR0wBc" +
            "MZ6XOnhRfIkjRFqRVKOYhWqJdqiwdZqGrxWfnujo9VY99xtmR9hr2xtGDBognpbt8Ay/k0du/p4q416i+5PXIn+RcyPbIu3mMOOf" +
            "HVMMp+tF2lz78S41+1O5i0YXpR0oMYv5jKeXjQDchAzPhIMsb9LQxpexbLwCr6jJUcxm7DuhBf0w1OZ7///ocXwd";
        internal const string RecordsLp2 =
            "EgAAAAQAAGEs+771qCO/cQl2AB7qpTrTH9pBN/U2XSCxuaNpzm6VGkjIjX35Bl6HNjqrFiLUtRxXDDHbr378LbO2fE4WMp8IWwLr" +
            "/8H1Cu1AGnxMBj8kInOe9PXqXY2wPDtQpvsL0aDwKg1lxZLc6QuGGs6CV7jaalqqjvl/d61GmwE999uPwSt7OlOxHf0Sqow5plKJ" +
            "mC8nRSg/Z8QudngBfLRHPAFAPpV1RsPiOBTMCwH8un2pWZk7e2zcW0IyQ9UXV7gb+MRH9R/BVtQdumWx+5QxqZdY4wNuc/4jT1TX" +
            "1OK86Ark/kFNiYezZTGNSdHpmO+LY1ZtTfg1jWy5vdSamDsNF1kLsEBehYEV2tcVfcx6hpLIYUO4zCXEGmcm9mbz9esw7adqKXkK" +
            "LmOJT2/c7cLPhY35Iwv7gXFZAyWdi51LqJ/kAqMOw6GkdZZe4Bs2OP3Gcijr7PfdgRfuGhyM3dVLRwxsfbaIhNnfo3ypXqZLamx4" +
            "556q7L8EaWSBVfjpqsLPL8qoWGzGrnQXpjmZL/4Zt3Zay/IQ4GOG15XTEuZNanxy6f/UzPFFWJm20p3hsKq+LafVVbZJZ7liiSiu" +
            "ZVctD82n8g9R0TYQt/BsjdjwpcByAYSsVoXTuGhv9zDKOAMh1Q5draeB6955eErUsYMHLSx9vIJ9DpfRcq4dQ9nA5FdORp6XIHuR" +
            "TAIC9aibxBmCy3zHJU7/owxkyogJd6VYaq80FUzRTZFnl3A0P8+9MeiogukWWyTUbbarF3eUos70KHizSDkUVdwTvQ5emGYE2lfB" +
            "Z4OnArJIonw0Hooa+pSQ/pvF6HSInMXbnpyy1AljNrlCVY+BXOeVBEo/7N+CFusr8k7Y50MtDE1Dj25fDNsazbxJicN6Ym/yhvtZ" +
            "fL8MOVD6VPBs4I4ydz+VAwwT6RfSwgoLvbZ6QQUTj28ejSilzSwuN8MKpFgj18rSIPOiHPmBBzb4726Un7xODQhguqTBhTswIGir" +
            "m0rZ3+SkDxPbNfxoMWcOGV3m7AVuIomlxf3wHIe4bqgrQvEKfoJvcz03hDU2ZYQ+Qcq/yjgSUTkc41jwZmqSp14oTtIcWyj8Jfah" +
            "hIBxzhcnqzi5Cn52BDFN698spndbBaMFrd+z3wG1gl8K36VgtXSs+1IrK2xKuw27+klHSbH1WixaCJbMKWrT43kvCJKaCBNKbRKH" +
            "qpGsnZ5GIwt4LJbaFqYqbwX2bsOmq+/DFoqJjHzkM64qc8FjDPHIQK+bxZE3tLT4nH0mzmWvyzLK9fedj3068ihaEGvTFZ0gpRhH" +
            "T/+ZVH0xRTw5TSwOR/09h/KvuNbBC5A3Fq6Ar7knepQRHXKZaUlbqD02GgTwF8AwxvaUfWlJZY+AvqNeoR1TFqr8Cv2zarpI+OAl" +
            "7Mf59PirzYQzZ0i69mRWnKeNJ9OCOKX7wtTPOdjswCB7BGxBfNScmHZjkbUeJX0oCrGEps2vw+zvnPS6gWt/nDdgF4yM9gHewO48" +
            "dk8gFOXz5os2lZL3ZAChBqSTJcXPm2kTR++bt8Xq98q/kC6qje24bOGWN5xnumPre8HfEKlaK5/Eo+mFXF7eHNU7yucxvvA/8mIE" +
            "w5opKrfDt/xQXh114oDxEwEsGPKUlMOUKaI4QTHEupxMPKYdxUxk2f7AxcAbsgsxt6iPKI+bOJs7dTvIriWKFhzd1hOcfSSL/ON2" +
            "shJ8smZMyEa3MaF7IkqCFBcgQydTe/qhy0jkHTPqwYDqVeuktRaZvxBrqVCn/YK+lmDFeg2qwyuP0P9c41VKZEqdqKnGrP6hE/+g" +
            "G1qv+O0SJJ0lxFqqQYnfwpMd9INo4tmLsTyQkIT8/cuPb6fEnUNO3FaCfNNjpu4yeisvO1aLnKj1LEq7QZNx91Ag5n7KgOYSvdnQ" +
            "GpoeZqzeEbDki1FWSxnxjllNB5i6qUhL0mPOsD/WCuHTzHbJr9MvoLQHSJ1/uPdv1AwYIi1CvJWz/7UlgyLDAW8+fyGvlPRQEpuH" +
            "08CRdOGDrLqf4MEx6vCqeBjEek4iDJOqA5C5oojqU11pzC8KIoU5qhJAFPqico3xjf1M/o1R3dPev5FDWdKOc/GKmGAi6bRVN8rh" +
            "DZkgzEqM0XHs+TXwcPYtOiaisJmLqxU/7/oZYzXe14us4JHhI3unrVAe9VVBpVvZJDBibPNfST+mfFbRxjhf3ARz+lr0dQwQwszP" +
            "AwsR9lAPyk19jzldWTulybQOkByYcJeXPtrsxLz+uNCnsUIAxeqx/y/5CwvMgdJktqlsql5TcXULsfbRTgOYt2OUlwLA+LU8IUFx" +
            "tPAdyb//o5sNBxmPp9lRP48sC6fF9mEkXCY3Stx4SR9RAqZISCQnPzCwPe7pQdRD14I2jfekENMkojRsjGmb84WfKfhLLkqRBDPC" +
            "p5Q7sBwNOSXOGzQQm9+aUs+Q876NXyE+ib6Xg6oRIUw2dFd077NpK5mbLT5suiSnh4dgfKCpwBYci6bJQnuqv1pn0WZ0P58BDVOt" +
            "ePlYfUYW4U6b4sjWrBPur027wO+csZ19wGJfaMhM4yb8iY5qJX8gEzah0+nWt+NgUn5XSbWh4f/qzVMpIkqzrj2aBQEbNehjFszP" +
            "EuabHbMUwlUc3OJYF5f9A+t3wEN8CGq2u9yqoS+q957chnCXLYfvB8GRKEJh1n65soM/w9HPxMiBg8imgGwex1AygKlDWFG4Hrj8" +
            "cJQR3clSW6whSNGhFemvrvEgU/f93Wfa9ddEHmFOCgTeQ2SOqdTesVOSE6YQqsfbQemIdBDY8tzxOoYx3GF9jrl30tvsoQ92pO1l" +
            "4u0lr62usoSO8oWWW9HBR0t97hN3GTpWCLL5TdGRRF8slD2+/BUD4ZC6S/9/OBO2pkbL1iD3g+n2QlUrlWS2lYX3uoSehmZk4RnB" +
            "h5C3XHK8spCtTFoEvFdp8QhDyhEFUd93Tqiy/z0aom1Gpj9wpM++DkV8coGlSJqrC1hEZI6QlL2boCUeC4FqONK3x0Nn8LikzZbm" +
            "K6t2bS12GZbIVKKIZvQPG1Sf6iE+iKKYUsBUdI2/WjmpOafBEpml6Ojr4hfFPRrpyM4VvhpCU/mHdaPmifk77L5hWcQ7FD35ozq5" +
            "f5TY/IxV/eiCIySK/VCjUg6cQqm9u/hyrpQcwBPnmUhntge3HVDv6/o2rCem0PNfhXDYTW3zMgVhf1XDtUImKUBA0GH/T/WnH9w1" +
            "8WQ/1lBf2FlIb5/FMPx7ljeHzkx+CI72AN8aI0gF+ZNREWm+KAm247wQP5yXws90fe/if+WMst6vpPX6aANi5Ve9D0mF8KxpNxot" +
            "aykG30zSNqz6tib204VQZHBdJNQ5Hn+/5zfJfdLTQN8ncahWnammJV8w7UfCYh1VE7ixxqp0BtbMje3sJZz0XZ/e+AtTLpMF7S2H" +
            "wvR4VKo2dYbzMPiypvO+4Jboon3Mexb6zoM5Q/3CR2gCaRU3joiwchKIySd0XS2qvLkGLphQjg0EPvi51ZL1VEDOCh5U0wufrIuV" +
            "/20Cajh6HJRIvjTBxrWZOT9VCsyawc7hi6NTFPkXQQidVBudoKBAMbuo/t/GZT2TdUKyt13CiiZmocrEnDg8fnuPkvaG6Y+EFBK8" +
            "wbOycuy+J1ZKYrs8RX6PhBENWe8kulDmSyGcvekEEsb78vITTsBwx41RrAqFYUmmhNu4gOA6ddg3BpAKiZGlpxsKCiCJobezEWIq" +
            "uq4i+MpsS4nq5UATfU8tBCzM6PUtKAVq28zawau2r2YZKnQjfM3rWieM2HEkFn6F7Eng6NhrrdtGVkt1wUGHEq5DNjD+VNOvEJdr" +
            "7rsyX+2XXyVRj6odN2Jmvo872edoOxcwg3N9y8EOyEsd/jYYtIwIYZwSAaU2CWiXli0bGXLGx6/dXuxi8eAI/+tSKpV/UsO1t+HD" +
            "gP288L8n+bdlGpyCZqfxVOzzM1fYGmKE4161HR1Zx/4v1g/jBmSVLip0fIhzKajx7oU3KKOnqBir1fj+ynEDjFp1X38XcswQusY/" +
            "0hYu0nuXBoEnn6pbgAVnp7t/Gj2MmwGTJPtCD6ZMva/0GGMV7dsL51ki4dHNhROQIjRnUf4XusQhw60eF1psynNWnbFYpIOT18Zv" +
            "v+VP36VPkrCHjhdsOnahPFWLgieG5CzVnWZbM0ODzO5XObSvIt21eb5cvjLUUUeubsyPjYWnZVVUNQV+uKk+wOK5u50s67I8Pys8" +
            "9DXb+av47VhwER+NZA9jy1xhX5OkZqk47FINNslOyeu2B8aHwOggqt1cWlejbYULUuZV+Qa+0TZzMo71EILTZ/+a0G+PzO984TKO" +
            "/PHNjheYi0m5GcXMSkCGKcxZx0Hnrz0lGOLKeSfZbbGsnmpAUOVhiMPTOcP8/TzHMKY4Y4zb6+VZM/y1enrL77HrrgKMaYPdJIse" +
            "0fyLBU8q99aXla+y0dI1cuJikiCFwkxz0OZv8/gbn0FvUv/8bsoSPW2W6t2d56GdHDf9OVNMpnhQCDo22/hQjjvQHkHJahPGMNlo" +
            "XDLvnBZKJmDupWo2ffvLz9aMcTz2LBnUCpWfAXx7MtL1tuXPOxMAV82lbUzEutVQ1TmSvoTk8fL1g6KiuJCJLGRj/H1iXr3xJ1z/" +
            "bqUQgzZvOdeYnTZwK3+bIugC70FjnyHm5we/KJyRMFotekaEqq8O2V3ot42bvM4BXyBSdB4J1LeCgkw5IiXFkoyPC6LIdgTlU4e4" +
            "hjjOCA24kCvCcwURXTzdMLB1ddsjTqOJ610KkAd6Ui0n3wG+Ep4Nc/ueZ5Ru7oXaUdJU21Z0/pq/EjF7aVpAakGdJuiKCCTNRjfV" +
            "SKK4VWyty87x6T/SaEi3FyWS9lzY6GcWzPKvskelzUgmyt4I6leC4MEiwAr2ZRSaEhfTOxsZ16wUKyLq+/a4Uxwaiai24UYTaltv" +
            "mQNpi10ThieGbyk9y/XeF4SWHJx7bECeUH6is7+Ws9xj+DhMndHFnz+GpF07sWjuLUu5t0yNK/Nb5GPsIEZwf2rmz/Tx5lJcQMmW" +
            "vbRZR8ncP5XLJvVZOu5IE87iOJTgJM9wBaFRJjZBVTmKnguJOCB7BfMIpHF/8fe0EVz57etvPECC2XTayUlONfE2sjEp8v8Dbycp" +
            "8R735inMSpw6z1u8DFyupj+YAEtMWF//BJ8oAA==";
    }
}
