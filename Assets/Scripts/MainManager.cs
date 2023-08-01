using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;
using SocketIO;
using System.Net.Sockets;
using System.Net;

public class MainManager : MonoBehaviour
{
    public GameObject shop_open_popup;
    public GameObject decarbonate_popup;

    //work
    public GameObject workObj;
    public RawImage[] wineBack;
    public GameObject[] priceObj;
    public Text[] priceTxt;

    public GameObject socketPrefab;
    GameObject socketObj;
    SocketIOComponent socket;

    public GameObject err_popup;
    public Text err_content;

    public GameObject serverErrPopup;
    public Text server_err_content;

    //setting
    public GameObject settingObj;
    public InputField appNo;
    public InputField ip;

    public GameObject washPopup;
    public GameObject bottlePopup;
    public GameObject bottleInitPopup;
    public GameObject devicecheckingPopup;

    public AudioSource[] soundObjs; //0-sound, 1-alarm, 2-touch, 3-start_app
    int curWineIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        soundObjs[3].Play();
        if (Global.ip == "" || Global.pInfo[0].serial_number == 0)
        {
            ShowSettingScene();
        }
        else
        {
            checkIP();
        }
    }

    void checkIP()
    {
        WWWForm form = new WWWForm();
        form.AddField("serial_number", Global.pInfo[0].serial_number);
        form.AddField("type", 2);
        WWW www = new WWW(Global.api_url + Global.check_db_api, form);
        StartCoroutine(ipCheck(www));
    }

    void ShowSettingScene(bool is_set = true)
    {
        workObj.SetActive(!is_set);
        settingObj.SetActive(is_set);
        ip.text = Global.ip;
        Debug.Log("serial:" + Global.pInfo[0].serial_number);
        appNo.text = Global.pInfo[0].serial_number.ToString();
    }

    public void onBack()
    {
        Debug.Log(Global.ip + ", " + Global.pInfo[0].serial_number);
        if(Global.ip == "" || Global.pInfo[0].serial_number == 0)
        {
            err_content.text = "설정값들을 정확히 입력하세요.";
            err_popup.SetActive(true);
        }
        else
        {
            ShowSettingScene(false);
            showWorkScene();
            curWineIndex = 0;
            tagControl(1);
            curWineIndex = 1;
            tagControl(1);
        }
    }

    IEnumerator ipCheck(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            Debug.Log(jsonNode);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                if (socket != null)
                {
                    socket.Close();
                    socket.OnDestroy();
                    socket.OnApplicationQuit();
                }
                if (socketObj != null)
                {
                    DestroyImmediate(socketObj);
                }
                settingObj.SetActive(false);
                workObj.SetActive(true);
                JSONNode tapList = JSON.Parse(jsonNode["tapList"].ToString()/*.Replace("\"", "")*/);
                Debug.Log(tapList);
                try
                {
                    for (int i = 0; i < tapList.Count; i++)
                    {
                        Global.pInfo[i].id = tapList[i]["id"].AsInt;
                        Global.pInfo[i].serial_number = tapList[i]["serial_number"].AsInt;
                        Global.pInfo[i].server_id = tapList[i]["server_id"].AsInt;
                        Global.pInfo[i].is_soldout = tapList[i]["is_soldout"].AsInt;
                        Global.pInfo[i].cup_size = tapList[i]["cup_size"].AsInt;
                        Global.pInfo[i].unit_price = tapList[i]["unit_price"].AsInt;
                        Global.pInfo[i].open_time = tapList[i]["opentime"].AsInt;
                        Global.pInfo[i].sell_type = tapList[i]["sell_type"].AsInt;
                        Global.pInfo[i].decarbo_time = tapList[i]["decarbo_time"].AsInt;
                        Global.pInfo[i].total_amount = tapList[i]["total_amount"].AsInt;
                        Global.pInfo[i].remain_amount = tapList[i]["remain_amount"].AsInt;
                        Global.pInfo[i].decarbonation = tapList[i]["decarbonation"].AsInt;
                        Global.pInfo[i].board_no = tapList[i]["board_no"].AsInt;
                        Global.pInfo[i].board_channel = tapList[i]["board_channel"].AsInt;
                        Global.pInfo[i].tagGW_no = tapList[i]["gw_no"].AsInt;
                        Global.pInfo[i].tagGW_channel = tapList[i]["gw_channel"].AsInt;
                        if(Global.pInfo[i].is_soldout == 1)
                        {
                            Global.pInfo[i].sceneType = WorkSceneType.soldout;
                        }
                        else
                        {
                            Global.pInfo[i].sceneType = WorkSceneType.standby;
                        }
                        string url = Global.image_server_path + "Standby" + jsonNode["server_id"].AsInt + ".jpg";
                        StartCoroutine(downloadFile(url, Global.imgPath + Path.GetFileName(url)));
                        curWineIndex = i;
                        showWorkScene();
                    }
                }
                catch(Exception ex)
                {
                }
                string downloadImgUrl = Global.image_server_path + "Pour.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
                downloadImgUrl = Global.image_server_path + "Remain.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
                downloadImgUrl = Global.image_server_path + "Soldout.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
                downloadImgUrl = Global.image_server_path + "tap.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
                InitSocketFunctions();
            }
            else
            {
                ShowSettingScene();
            }
        }
        else
        {
            ShowSettingScene();
        }
    }

    void showWorkScene(int capacity = 0)
    {
        try
        {
            switch (Global.pInfo[curWineIndex].sceneType)
            {
                case WorkSceneType.standby:
                    {
                        soundObjs[0].Play();
                        priceObj[curWineIndex].SetActive(true);
                        if (Global.pInfo[curWineIndex].sell_type == 0)
                        {
                            //cup
                            priceTxt[curWineIndex].text = Global.GetPriceFormat(Global.pInfo[curWineIndex].unit_price * Global.pInfo[curWineIndex].cup_size) + " 원/" + Global.GetPriceFormat(Global.pInfo[curWineIndex].cup_size) + "ml";
                        }
                        else
                        {
                            //ml
                            priceTxt[curWineIndex].text = Global.GetPriceFormat(Global.pInfo[curWineIndex].unit_price) + " 원/ml";
                        }
                        string downloadImgUrl = Global.image_server_path + "Standby" + Global.pInfo[curWineIndex].server_id + ".jpg";
                        string failImgUrl = Global.image_server_path + "tap.jpg";
                        string filepath = Global.imgPath + Path.GetFileName(downloadImgUrl);
                        StartCoroutine(downloadAndLoadImage(downloadImgUrl, filepath, wineBack[curWineIndex]));
                        StartCoroutine(checkDownImage(filepath, failImgUrl));
                        break;
                    };
                case WorkSceneType.soldout:
                    {
                        soundObjs[1].Play();
                        string downloadImgUrl = Global.image_server_path + "Soldout.jpg";
                        string filepath = Global.imgPath + Path.GetFileName(downloadImgUrl);
                        StartCoroutine(downloadAndLoadImage(downloadImgUrl, filepath, wineBack[curWineIndex]));
                        priceObj[curWineIndex].SetActive(false);
                        break;
                    };
                case WorkSceneType.remain:
                    {
                        string downloadImgUrl = Global.image_server_path + "Remain.jpg";
                        string filepath = Global.imgPath + Path.GetFileName(downloadImgUrl);
                        StartCoroutine(downloadAndLoadImage(downloadImgUrl, filepath, wineBack[curWineIndex]));
                        priceTxt[curWineIndex].text = Global.GetPriceFormat(capacity) + " 원";
                        priceObj[curWineIndex].SetActive(true);
                        break;
                    };
                case WorkSceneType.pour:
                    {
                        string downloadImgUrl = Global.image_server_path + "Pour.jpg";
                        string filepath = Global.imgPath + Path.GetFileName(downloadImgUrl);
                        StartCoroutine(downloadAndLoadImage(downloadImgUrl, filepath, wineBack[curWineIndex]));
                        priceTxt[curWineIndex].text = Global.GetPriceFormat(capacity) + " ml";
                        priceObj[curWineIndex].SetActive(true);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    IEnumerator checkDownImage(string imgPath, string failPath)
    {
        yield return new WaitForSeconds(3f);
        if (!File.Exists(imgPath))
        {
            string filepath = Global.imgPath + Path.GetFileName(failPath);
            Debug.Log(filepath);
            StartCoroutine(downloadAndLoadImage(failPath, filepath, wineBack[curWineIndex]));
        }
    }

    public void onDecarbonate()
    {
        StartCoroutine(Decarbonate(curWineIndex));
    }

    IEnumerator Decarbonate(int index)
    {
        Debug.Log("curWineIndex : " + index);
        decarbonate_popup.SetActive(true);
        valveControl(1, 1);
        yield return new WaitForSeconds(Global.pInfo[index].decarbo_time);
        tagControl(1);
        valveControl(1, 0);
        decarbonate_popup.SetActive(false);
        ShowSettingScene(false);
        Global.pInfo[index].sceneType = WorkSceneType.standby;
        curWineIndex = index;
        showWorkScene();
    }

    public void SaveSetInfo()
    {
        if(ip.text == "")
        {
            err_content.text = "ip를 입력하세요.";
            err_popup.SetActive(true);
        }
        else if (appNo.text == "" || appNo.text == "0")
        {
            err_content.text = "기기번호를 입력하세요.";
            err_popup.SetActive(true);
        }
        else
        {
            try
            {
                string tmp_url = "http://" + ip.text.Trim() + ":" + Global.api_server_port + "/m-api/self/";
                WWWForm form = new WWWForm();
                form.AddField("serial_number", int.Parse(appNo.text));
                form.AddField("type", 2);
                WWW www = new WWW(tmp_url + Global.save_setinfo_api, form);
                StartCoroutine(saveSetProcess(www));
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
        }
    }

    void InitSocketFunctions()
    {
        socketObj = Instantiate(socketPrefab);
        socket = socketObj.GetComponent<SocketIOComponent>();
        socket.On("open", socketOpen);
        socket.On("soldout", soldoutEventHandler);
        socket.On("infochanged", InfoChangedEventHandler);
        socket.On("flowmeterStart", flowmeterStartEventHandler);
        socket.On("flowmeterValue", flowmeterValueEventHandler);
        socket.On("flowmeterFinish", flowmeterFinishEventHandler);
        socket.On("errorReceived", errorReceivedEventHandler);
        socket.On("adminReceived", adminReceivedEventHandler);
        socket.On("shopOpen", openShopEventHandler);
        socket.On("shopClose", closeShopEventHandler);
        socket.On("RepairingDevice", RepairingDevice);
        socket.On("error", socketError);
        socket.On("close", socketClose);
    }

    public void flowmeterStartEventHandler(SocketIOEvent e)
    {
        try
        {
            Debug.Log("[SocketIO] FlowmeterStartEvent received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int id = jsonNode["id"].AsInt;
            int index = -1;
            for (int i = 0; i < 2; i++)
            {
                if (Global.pInfo[i].serial_number == id)
                {
                    index = i; break;
                }
            }
            if (index == -1)
                return;
            curWineIndex = index;
            Global.pInfo[index].is_soldout = 0;
            Global.pInfo[index].sceneType = WorkSceneType.pour;
            showWorkScene(0);
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void closeShopEventHandler(SocketIOEvent e)
    {
        Debug.Log("shop Close event!");
        try
        {
            int index = -1;
            for (int i = 0; i < 2; i++)
            {
                Debug.Log("total:" + Global.pInfo[i].total_amount);
                if (Global.pInfo[i].total_amount == 0)
                    continue;
                int percentage = Global.pInfo[i].remain_amount * 100 / Global.pInfo[i].total_amount;
                Debug.Log("per:" + percentage);
                if (percentage < Global.pInfo[i].decarbonation)
                {
                    index = i; break;
                }
            }
            Debug.Log("index:" + index);
            if (index == -1)
                return;
            curWineIndex = index;
            decarbonate_popup.SetActive(true);
            StartCoroutine(shopDecarbonate(curWineIndex));
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    public void errorReceivedEventHandler(SocketIOEvent e)
    {
        //type
        try
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int id = jsonNode["id"].AsInt;
            int index = -1;
            for (int i = 0; i < 2; i++)
            {
                if (Global.pInfo[i].serial_number == id)
                {
                    index = i; break;
                }
            }
            if (index == -1)
                return;
            curWineIndex = index;
            int type = jsonNode["type"].AsInt;
            if (type == 1)
            {
                server_err_content.text = jsonNode["content"];
                serverErrPopup.SetActive(true);
                StartCoroutine(closeServerErrPopup());
            }
            else
            {
                int is_close = jsonNode["is_close"].AsInt;
                if (is_close == 1)
                {
                    serverErrPopup.SetActive(false);
                }
                else
                {
                    err_content.text = jsonNode["content"];
                    serverErrPopup.SetActive(true);
                }
            }
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    IEnumerator closeServerErrPopup()
    {
        yield return new WaitForSeconds(3f);
        serverErrPopup.SetActive(false);
        ShowSettingScene(false);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
        showWorkScene();
    }

    public void adminReceivedEventHandler(SocketIOEvent e)
    {
        try
        {
            Debug.Log("[SocketIO] AdminReceivedEvent received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int id = jsonNode["id"].AsInt;
            int index = -1;
            for (int i = 0; i < 2; i++)
            {
                if (Global.pInfo[i].serial_number == id)
                {
                    index = i; break;
                }
            }
            if (index == -1)
                return;
            curWineIndex = index;
            ShowSettingScene();
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    IEnumerator saveSetProcess(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if(result == 1)
            {
                if(Global.ip != ip.text)
                {
                    Global.ip = ip.text;
                    Global.api_url = "http://" + Global.ip + ":" + Global.api_server_port + "/m-api/self/";
                    Global.socket_server = "ws://" + Global.ip + ":" + Global.api_server_port;
                    Global.image_server_path = "http://" + Global.ip + ":" + Global.api_server_port + "/self/";
                    string _pourImgUrl = Global.image_server_path + "Pour.jpg";
                    string _remainImgUrl = Global.image_server_path + "Remain.jpg";
                    string _soldoutImgUrl = Global.image_server_path + "Soldout.jpg";
                    string _tapImgUrl = Global.image_server_path + "tap.jpg";
                    if (File.Exists(_pourImgUrl))
                    {
                        File.Delete(_pourImgUrl);
                    }
                    if (File.Exists(_remainImgUrl))
                    {
                        File.Delete(_remainImgUrl);
                    }
                    if (File.Exists(_soldoutImgUrl))
                    {
                        File.Delete(_soldoutImgUrl);
                    }
                    if (File.Exists(_tapImgUrl))
                    {
                        File.Delete(_tapImgUrl);
                    }
                    if (socket != null)
                    {
                        socket.Close();
                        socket.OnDestroy();
                        socket.OnApplicationQuit();
                    }
                    if (socketObj != null)
                    {
                        DestroyImmediate(socketObj);
                    }
                    InitSocketFunctions();
                }
                Global.pInfo[0].serial_number = int.Parse(appNo.text);
                Debug.Log("set_serial:" + Global.pInfo[0].serial_number);
                PlayerPrefs.SetInt("appNo", Global.pInfo[0].serial_number);
                PlayerPrefs.SetString("ip", Global.ip);
                Global.pInfo[1].serial_number = int.Parse(appNo.text) + 1;

                JSONNode tapList = JSON.Parse(jsonNode["tapList"].ToString()/*.Replace("\"", "")*/);
                try
                {
                    for (int i = 0; i < tapList.Count; i++)
                    {
                        Global.pInfo[i].id = tapList[i]["id"].AsInt;
                        Global.pInfo[i].server_id = tapList[i]["server_id"].AsInt;
                        Global.pInfo[i].is_soldout = tapList[i]["is_soldout"].AsInt;
                        Global.pInfo[i].cup_size = tapList[i]["cup_size"].AsInt;
                        Global.pInfo[i].unit_price = tapList[i]["unit_price"].AsInt;
                        Global.pInfo[i].open_time = tapList[i]["opentime"].AsInt;
                        Global.pInfo[i].sell_type = tapList[i]["sell_type"].AsInt;
                        Global.pInfo[i].decarbo_time = tapList[i]["decarbo_time"].AsInt;
                        Global.pInfo[i].total_amount = tapList[i]["total_amount"].AsInt;
                        Global.pInfo[i].remain_amount = tapList[i]["remain_amount"].AsInt;
                        Global.pInfo[i].decarbonation = tapList[i]["decarbonation"].AsInt;
                        Global.pInfo[i].board_no = tapList[i]["board_no"].AsInt;
                        Global.pInfo[i].board_channel = tapList[i]["board_channel"].AsInt;
                        Global.pInfo[i].tagGW_no = tapList[i]["gw_no"].AsInt;
                        Global.pInfo[i].tagGW_channel = tapList[i]["gw_channel"].AsInt;
                        if (Global.pInfo[i].is_soldout == 1)
                        {
                            Global.pInfo[i].sceneType = WorkSceneType.soldout;
                        }
                        else
                        {
                            Global.pInfo[i].sceneType = WorkSceneType.standby;
                        }
                        string url = Global.image_server_path + "Standby" + tapList[i]["server_id"].AsInt + ".jpg";
                        //StartCoroutine(downloadFile(url, Global.imgPath + Path.GetFileName(url)));
                        string failImgUrl = Global.image_server_path + "tap.jpg";
                        string filepath = Global.imgPath + Path.GetFileName(url);
                        StartCoroutine(downloadAndLoadImage(url, filepath, wineBack[i]));
                        StartCoroutine(checkDownImage(filepath, failImgUrl));
                    }
                }
                catch (Exception ex)
                {
                }
                string downloadImgUrl = Global.image_server_path + "Pour.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
                downloadImgUrl = Global.image_server_path + "Remain.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
                downloadImgUrl = Global.image_server_path + "Soldout.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
                downloadImgUrl = Global.image_server_path + "tap.jpg";
                StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));

                err_popup.SetActive(true);
                err_content.text = "저장되었습니다.";
            }
            else
            {
                err_content.text = "정보를 정확히 입력하세요.";
                err_popup.SetActive(true);
            }
        }
        else
        {
            err_content.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            err_popup.SetActive(true);
        }
    }

    public void onConfirmSavePopup()
    {
        err_popup.SetActive(false);
    }

    public void Wash()
    {
        washPopup.SetActive(true);
        valveControl(0, 1);
    }

    public void onConfirmWashPopup()
    {
        washPopup.SetActive(false);
        //ShowSettingScene(false);
        //Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
        //showWorkScene();
        valveControl(0, 0);
    }

    public void BottleChange()
    {
        bottlePopup.SetActive(true);
        //tagControl(0);
        valveControl(0, 1);
    }

    public void onConfirmBottlePopup()
    {
        bottlePopup.SetActive(false);
        bottleInitPopup.SetActive(true);
    }

    IEnumerator ProcessBottleInitConfirmApi()
    {
        WWWForm form = new WWWForm();
        form.AddField("serial_number", Global.pInfo[curWineIndex].serial_number);
        WWW www = new WWW(Global.api_url + Global.bottle_init_confirm_api, form);
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            string result = jsonNode["suc"].ToString()/*.Replace("\"", "")*/;
            if (result == "1")
            {
                ShowSettingScene(false);
                Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
                showWorkScene();
                bottleInitPopup.SetActive(false);
                err_popup.SetActive(false);
                tagControl(1);
                valveControl(0, 0);
            }
        }
        else
        {
            bottleInitPopup.SetActive(false);
            err_content.text = "서버와 연결 중입니다. 잠시만 기다려주세요.";
            err_popup.SetActive(true);
        }
    }

    public void onConfirmBottleInitPopup()
    {
        bottleInitPopup.SetActive(false);
        err_popup.SetActive(false);
        valveControl(0, 0);
        tagControl(1);
        ShowSettingScene(false);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
        showWorkScene();
        //StartCoroutine(ProcessBottleInitConfirmApi());
    }

    public void onCancelBottleInitPopup()
    {
        valveControl(0, 0);
        bottleInitPopup.SetActive(false);
    }

    public void Soldout()
    {
        WWWForm form = new WWWForm();
        form.AddField("serial_number", Global.pInfo[curWineIndex].serial_number);
        WWW www = new WWW(Global.api_url + Global.soldout_api, form);
        StartCoroutine(SoldoutProcess(www));
    }

    IEnumerator SoldoutProcess(WWW www)
    {
        yield return www;
        if(www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if(result == 1)
            {
                ShowSettingScene(false);
                Global.pInfo[curWineIndex].is_soldout = 1;
                Global.pInfo[curWineIndex].sceneType = WorkSceneType.soldout;
                showWorkScene();
            }
            else
            {
                err_content.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
                err_popup.SetActive(true);
            }
        }
        else
        {
            err_content.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            err_popup.SetActive(true);
        }
    }

    void tagControl(int status)
    {
        if(socket != null)
        {
            string tagGWData = "{\"tagGW_no\":\"" + Global.pInfo[curWineIndex].tagGW_no + "\"," +
                "\"ch_value\":\"" + Global.pInfo[curWineIndex].tagGW_channel + "\"," +
                "\"status\":\"" + status + "\"}";
            socket.Emit("deviceTagLock", JSONObject.Create(tagGWData));
        }
    }

    void valveControl(int valve, int status)
    {
        if (socket != null)
        {
            string data = "{\"board_no\":\"" + Global.pInfo[curWineIndex].board_no + "\"," +
                "\"ch_value\":\"" + Global.pInfo[curWineIndex].board_channel + "\"," +
                "\"valve\":\"" + valve + "\"," +
                "\"status\":\"" + status + "\"}";
            socket.Emit("boardValveCtrl", JSONObject.Create(data));
        }
    }

    public void socketOpen(SocketIOEvent e)
    {
        if (is_socket_open)
        {
            return;
        }
        if(Global.pInfo[0].serial_number != 0 && socket != null)
        {
            is_socket_open = true;
            string sId = "{\"no\":\"" + Global.pInfo[0].serial_number + "\"}";
            socket.Emit("self2SetInfo", JSONObject.Create(sId));
            Debug.Log("[SocketIO] Open received: " + e.name + " " + e.data);
        }
    }

    public void InfoChangedEventHandler(SocketIOEvent e)
    {
        JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
        int s_number = jsonNode["serial_number"].AsInt;
        int selected_index = -1;
        for (int i = 0; i < Global.pInfo.Length; i++)
        {
            if (s_number == Global.pInfo[i].serial_number)
            {
                selected_index = i;
                break;
            }
        }
        if (selected_index < 0)
        {
            return;
        }
        curWineIndex = selected_index;
        try
        {
            Global.pInfo[selected_index].id = jsonNode["id"].AsInt;
            Global.pInfo[selected_index].server_id = jsonNode["server_id"].AsInt;
            Global.pInfo[selected_index].open_time = jsonNode["opentime"].AsInt;
            Global.pInfo[selected_index].sell_type = jsonNode["sell_type"].AsInt;
            Global.pInfo[selected_index].cup_size = jsonNode["cup_size"].AsInt;
            Global.pInfo[selected_index].unit_price = jsonNode["unit_price"].AsInt;
            Global.pInfo[selected_index].decarbo_time = jsonNode["decarbo_time"].AsInt;
            Global.pInfo[selected_index].total_amount = jsonNode["total_amount"].AsInt;
            Global.pInfo[selected_index].remain_amount = jsonNode["remain_amount"].AsInt;
            Global.pInfo[selected_index].decarbonation = jsonNode["decarbonation"].AsInt;
            Global.pInfo[selected_index].board_no = jsonNode["board_no"].AsInt;
            Global.pInfo[selected_index].board_channel = jsonNode["board_channel"].AsInt;
            Global.pInfo[selected_index].tagGW_no = jsonNode["gw_no"].AsInt;
            Global.pInfo[selected_index].tagGW_channel = jsonNode["gw_channel"].AsInt;
            Global.pInfo[selected_index].is_soldout = jsonNode["soldout"].AsInt;
            if (jsonNode["soldout"].AsInt == 1)
            {
                Global.pInfo[selected_index].sceneType = WorkSceneType.soldout;
            }
            else
            {
                Global.pInfo[selected_index].sceneType = WorkSceneType.standby;
            }
            string downloadImgUrl = Global.image_server_path + "Standby" + jsonNode["server_id"].AsInt + ".jpg";
            StartCoroutine(downloadFile(downloadImgUrl, Global.imgPath + Path.GetFileName(downloadImgUrl)));
            showWorkScene();
        }
        catch(Exception ex)
        {
            Debug.Log(ex);
        }
    }

    public void openShopEventHandler(SocketIOEvent e)
    {
        Debug.Log("shop open event.");
        shop_open_popup.SetActive(true);
        StartCoroutine(waitShopOpen1());
        StartCoroutine(waitShopOpen2());
    }

    IEnumerator waitShopOpen1()
    {
        yield return new WaitForSeconds(Global.pInfo[0].open_time);
        closeshopOpenPopup(0, false);
        shop_open_popup.SetActive(false);
    }

    IEnumerator waitShopOpen2()
    {
        yield return new WaitForSeconds(Global.pInfo[1].open_time);
        closeshopOpenPopup(1, false);
        shop_open_popup.SetActive(false);
    }

    IEnumerator shopDecarbonate(int index)
    {
        Debug.Log("stop decarbonate from shop close event.");
        yield return new WaitForSeconds(Global.pInfo[index].decarbo_time);
        decarbonate_popup.SetActive(false);
        ShowSettingScene(false);
        Global.pInfo[index].sceneType = WorkSceneType.standby;
        curWineIndex = index;
        showWorkScene();
    }

    public void RepairingDevice(SocketIOEvent e)
    {
        try
        {
            Debug.Log("[SocketIO] Repairing Device Event received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int id = jsonNode["id"].AsInt;
            int index = -1;
            for (int i = 0; i < 2; i++)
            {
                if (Global.pInfo[i].serial_number == id)
                {
                    index = i; break;
                }
            }
            if (index == -1)
                return;
            curWineIndex = index;
            devicecheckingPopup.SetActive(true);
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void soldoutEventHandler(SocketIOEvent e)
    {
        try
        {
            Debug.Log("[SocketIO] Soldout received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int id = jsonNode["id"].AsInt;
            int index = -1;
            for(int i = 0; i < 2; i++)
            {
                if(Global.pInfo[i].serial_number == id)
                {
                    index = i;break;
                }
            }
            if (index == -1)
                return;
            curWineIndex = index;
            ShowSettingScene(false);
            Global.pInfo[curWineIndex].is_soldout = 1;
            Global.pInfo[curWineIndex].sceneType = WorkSceneType.soldout;
            showWorkScene();
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void flowmeterValueEventHandler(SocketIOEvent e)
    {
        try
        {
            Debug.Log("[SocketIO] FlowmeterValueEvent received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int id = jsonNode["id"].AsInt;
            int index = -1;
            for (int i = 0; i < 2; i++)
            {
                if (Global.pInfo[i].serial_number == id)
                {
                    index = i; break;
                }
            }
            if (index == -1)
                return;
            curWineIndex = index;
            int value = jsonNode["value"].AsInt;
            priceTxt[curWineIndex].text = Global.GetPriceFormat(value) + " ml";
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    public void flowmeterFinishEventHandler(SocketIOEvent e)
    {
        try
        {
            Debug.Log("[SocketIO] FinishEvent received: " + e.name + " " + e.data);
            JSONNode jsonNode = SimpleJSON.JSON.Parse(e.data.ToString());
            int id = jsonNode["id"].AsInt;
            int index = -1;
            for (int i = 0; i < 2; i++)
            {
                if (Global.pInfo[i].serial_number == id)
                {
                    index = i; break;
                }
            }
            if (index == -1)
                return;
            curWineIndex = index;
            int type = jsonNode["type"].AsInt;//0-pour, 1-soldout, 2-remain
            int value = jsonNode["value"].AsInt;
            if (type == 1)
            {
                //soldout 완료
                priceTxt[curWineIndex].text = Global.GetPriceFormat(value) + " ml";
                StartCoroutine(GotoSoldout());
            }
            else if (type == 2)
            {
                //remain 완료
                Global.pInfo[index].sceneType = WorkSceneType.pour;
                showWorkScene(value);
                StartCoroutine(ReturntoRemain(jsonNode["remain_value"].AsInt));
            }
            else
            {
                //정상완료
                Global.pInfo[index].is_soldout = 0;
                int is_pay_after = jsonNode["is_pay_after"].AsInt;
                Global.pInfo[index].sceneType = WorkSceneType.pour;
                showWorkScene(value);
                if (is_pay_after == 1)
                {
                    //후불
                    StartCoroutine(ReturntoStandby());
                }
                else
                {
                    //선불
                    StartCoroutine(ReturntoRemain(jsonNode["remain_value"].AsInt));
                }
            }
        }
        catch (Exception err)
        {
            Debug.Log(err);
        }
    }

    IEnumerator GotoSoldout()
    {
        yield return new WaitForSeconds(1f);
        Global.pInfo[curWineIndex].is_soldout = 1;
        ShowSettingScene(false);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.soldout;
        showWorkScene();
    }

    IEnumerator ReturntoRemain(int remain_value)
    {
        yield return new WaitForSeconds(1f);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.remain;
        showWorkScene(remain_value);
        yield return new WaitForSeconds(3f);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
        showWorkScene();
    }

    IEnumerator ReturntoStandby()
    {
        yield return new WaitForSeconds(1f);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
        showWorkScene();
    }

    public void socketError(SocketIOEvent e)
    {
        Debug.Log("[SocketIO] Error received: " + e.name + " " + e.data);
    }

    public void socketClose(SocketIOEvent e)
    {
        is_socket_open = false;
        Debug.Log("[SocketIO] Close received: " + e.name + " " + e.data);
    }

    public void onConfirmDeviceCheckingPopup()
    {
        devicecheckingPopup.SetActive(false);
        tagControl(1);
        ShowSettingScene(false);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
        showWorkScene();
    }

    void closeshopOpenPopup(int index, bool is_manual = true)
    {
        Debug.Log("shop close event." + index + ", " + is_manual);
        if (is_manual)
        {
            tagControl(1);
            valveControl(0, 0);
        }
        curWineIndex = index;
        ShowSettingScene(false);
        Global.pInfo[index].sceneType = WorkSceneType.standby;
        showWorkScene();
    }

    public void onConfirmShopOpenPopup()
    {
        closeshopOpenPopup(0);
        closeshopOpenPopup(1);
        shop_open_popup.SetActive(false);
    }

    public void onConfirmDecarbonatePopup()
    {
        Debug.Log("confirm decarbonation");
        decarbonate_popup.SetActive(false);
        ShowSettingScene(false);
        Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
        showWorkScene();
        tagControl(1);
        valveControl(1, 0);
    }

    //download image
    IEnumerator downloadFile(string url, string pathToSaveImage)
    {
        yield return new WaitForEndOfFrame();
        if (!File.Exists(pathToSaveImage))
        {
            WWW www = new WWW(url);
            StartCoroutine(_downloadFile(www, pathToSaveImage));
        }
    }

    IEnumerator downloadAndLoadImage(string url, string pathToSaveImage, RawImage img)
    {
        try
        {
            if (img != null)
            {
                if (File.Exists(pathToSaveImage))
                {
                    StartCoroutine(LoadPictureToTexture(pathToSaveImage, img));
                }
                else
                {
                    WWW www = new WWW(url);
                    StartCoroutine(_downloadAndLoadImage(www, pathToSaveImage, img));
                }
            }
        }
        catch (Exception ex)
        {

        }
        yield return null;
    }

    private IEnumerator _downloadAndLoadImage(WWW www, string savePath, RawImage img)
    {
        yield return www;
        if (img != null)
        {
            //Check if we failed to send
            if (string.IsNullOrEmpty(www.error))
            {
                saveAndLoadImage(savePath, www.bytes, img);
            }
            else
            {
                UnityEngine.Debug.Log("Error: " + www.error);
            }
        }
    }

    void saveAndLoadImage(string path, byte[] imageBytes, RawImage img)
    {
        try
        {
            //Create Directory if it does not exist
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllBytes(path, imageBytes);
            Debug.Log("downloading1:" + path);
            //Debug.Log("Download Image: " + path.Replace("/", "\\"));
            StartCoroutine(LoadPictureToTexture(path, img));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed To Save Data to: " + path.Replace("/", "\\"));
            Debug.LogWarning("Error: " + e.Message);
        }
    }

    IEnumerator LoadPictureToTexture(string name, RawImage img)
    {
        //Debug.Log("load image = " + Global.prePath + name);
        WWW pictureWWW = new WWW(Global.prePath + name);
        yield return pictureWWW;
        try
        {
            if (img != null)
            {
                //img.sprite = Sprite.Create(pictureWWW.texture, new Rect(0, 0, pictureWWW.texture.width, pictureWWW.texture.height), new Vector2(0, 0), 8f, 0, SpriteMeshType.FullRect);
                img.texture = pictureWWW.texture;
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    private IEnumerator _downloadFile(WWW www, string path)
    {
        yield return www;
        //Check if we failed to send
        if (string.IsNullOrEmpty(www.error))
        {
            try
            {
                Debug.Log("downloading:" + path);
                //Create Directory if it does not exist
                if (!Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }
                File.WriteAllBytes(path, www.bytes);
                //Debug.Log("Download Image: " + path.Replace("/", "\\"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed To Save Data to: " + path.Replace("/", "\\"));
                Debug.LogWarning("Error: " + e.Message);
            }
        }
    }

    int order = 0;
    public void onClickOrder3()
    {
        if (order == 0)
        {
            order = 1;
        }
        else if (order == 1)
        {
            order = 2;
        }
        else if (order == 2)
        {
            order = 3;
        }
        else if (order == 3)
        {
            ShowSettingScene();
            curWineIndex = 0;
            tagControl(0);
            curWineIndex = 1;
            tagControl(0);
            order = 0;
        }
        else
        {
            order = 0;
        }
    }

    public void onClickOrder4()
    {
        if (Global.pInfo[curWineIndex].is_soldout == 1)
        {
            WWWForm form = new WWWForm();
            form.AddField("serial_number", Global.pInfo[curWineIndex].serial_number);
            WWW www = new WWW(Global.api_url + Global.cancel_soldout_api, form);
            StartCoroutine(CancelSoldout(www));
        }
    }

    IEnumerator CancelSoldout(WWW www)
    {
        yield return www;
        if (www.error == null)
        {
            JSONNode jsonNode = SimpleJSON.JSON.Parse(www.text);
            int result = jsonNode["suc"].AsInt;
            if (result == 1)
            {
                ShowSettingScene(false);
                Global.pInfo[curWineIndex].sceneType = WorkSceneType.standby;
                showWorkScene();
                tagControl(1);
                err_popup.SetActive(false);
            }
            else
            {
                err_content.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
                err_popup.SetActive(true);
            }
        }
        else
        {
            err_content.text = "서버와의 조작시 알지 못할 오류가 발생하였습니다.";
            err_popup.SetActive(true);
        }

    }

    float time = 0f;
    private bool is_socket_open = false;

    void FixedUpdate()
    {
        if (!Input.anyKey)
        {
            time += Time.deltaTime;
        }
        else
        {
            if (time != 0f)
            {
                soundObjs[2].Play();
                time = 0f;
            }
        }
    }
}
