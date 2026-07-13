package com.wms.mobile.data.session

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class EndpointPolicyTest {
    @Test
    fun `normaliza endpoint https y remueve slash final`() {
        assertEquals("https://wms.example.test", EndpointPolicy.normalize(" https://wms.example.test/ ").getOrThrow())
    }

    @Test
    fun `rechaza http remoto pero permite emulador local`() {
        assertTrue(EndpointPolicy.normalize("http://wms.example.test").isFailure)
        assertEquals("http://10.0.2.2:8080", EndpointPolicy.normalize("http://10.0.2.2:8080/").getOrThrow())
    }
}
