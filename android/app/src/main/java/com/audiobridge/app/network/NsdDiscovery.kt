package com.audiobridge.app.network

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.flowOn

data class DiscoveredService(
    val name: String,
    val host: String,
    val port: Int
)

class NsdDiscovery(private val context: Context) {

    private val nsdManager: NsdManager =
        context.getSystemService(Context.NSD_SERVICE) as NsdManager

    private var currentListener: NsdManager.DiscoveryListener? = null

    fun discover(): Flow<List<DiscoveredService>> = callbackFlow {
        val services = mutableListOf<DiscoveredService>()

        val listener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(serviceType: String) {}

            override fun onServiceFound(serviceInfo: NsdServiceInfo) {
                nsdManager.resolveService(serviceInfo, object : NsdManager.ResolveListener {
                    override fun onResolveFailed(info: NsdServiceInfo, errorCode: Int) {}

                    override fun onServiceResolved(info: NsdServiceInfo) {
                        val svc = DiscoveredService(
                            name = info.serviceName,
                            host = info.host?.hostAddress ?: "",
                            port = info.port
                        )
                        val i = services.indexOfFirst { it.name == svc.name }
                        if (i >= 0) services[i] = svc
                        else services.add(svc)
                        trySend(services.toList())
                    }
                })
            }

            override fun onServiceLost(serviceInfo: NsdServiceInfo) {
                services.removeAll { it.name == serviceInfo.serviceName }
                trySend(services.toList())
            }

            override fun onDiscoveryStopped(serviceType: String) {
                services.clear()
                trySend(emptyList())
            }

            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {}
            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {}
        }

        currentListener = listener
        nsdManager.discoverServices(
            ProtocolConstants.SERVICE_TYPE_NSD,
            NsdManager.PROTOCOL_DNS_SD,
            listener
        )

        awaitClose { stop() }
    }.flowOn(Dispatchers.IO)

    fun stop() {
        currentListener?.let {
            try {
                nsdManager.stopServiceDiscovery(it)
            } catch (_: Exception) {}
            currentListener = null
        }
    }
}
